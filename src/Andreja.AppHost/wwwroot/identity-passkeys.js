const antiforgeryHeader = "X-CSRF-TOKEN";

function statusElement() {
    return document.querySelector("[data-identity-status]");
}

function setStatus(message, isError = false) {
    const element = statusElement();
    if (!element) return;
    element.textContent = message;
    element.classList.toggle("error-message", isError);
}

function antiforgeryToken(form) {
    return form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
}

async function postJson(path, body, form) {
    const response = await fetch(path, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        headers: {
            "Content-Type": "application/json",
            [antiforgeryHeader]: antiforgeryToken(form)
        },
        body: JSON.stringify(body)
    });
    if (!response.ok) {
        throw new Error("identity-request-failed");
    }
    const contentType = response.headers.get("content-type") ?? "";
    return contentType.includes("application/json") ? response.json() : null;
}

function requirePasskeySupport() {
    if (!window.PublicKeyCredential
        || !PublicKeyCredential.parseCreationOptionsFromJSON
        || !PublicKeyCredential.parseRequestOptionsFromJSON) {
        throw new Error("passkeys-not-supported");
    }
}

async function createPasskey(optionsJson) {
    requirePasskeySupport();
    const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
    const credential = await navigator.credentials.create({ publicKey });
    if (!credential) throw new Error("passkey-cancelled");
    return JSON.stringify(credential.toJSON());
}

async function requestPasskey(optionsJson) {
    requirePasskeySupport();
    const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
    const credential = await navigator.credentials.get({ publicKey });
    if (!credential) throw new Error("passkey-cancelled");
    return JSON.stringify(credential.toJSON());
}

function showRecoveryCodes(result) {
    const section = document.querySelector("[data-recovery-codes]");
    const list = section?.querySelector("[data-recovery-code-list]");
    const confirm = section?.querySelector("[data-recovery-confirm]");
    const continueButton = section?.querySelector("[data-recovery-continue]");
    if (!section || !list || !confirm || !continueButton || !result.recoveryCodes) {
        window.location.assign(result.redirectUrl);
        return;
    }

    list.replaceChildren(...result.recoveryCodes.map(code => {
        const item = document.createElement("li");
        const value = document.createElement("code");
        value.textContent = code;
        item.append(value);
        return item;
    }));
    section.hidden = false;
    section.focus();
    section.dataset.redirectUrl = result.redirectUrl;
    continueButton.disabled = !confirm.checked;
}

function handleFailure(error) {
    const message = error?.message === "passkeys-not-supported"
        ? "This browser does not support the required passkey APIs."
        : "The identity request could not be completed. No secret was displayed.";
    setStatus(message, true);
}

function disableWhile(form, action) {
    const controls = [...form.querySelectorAll("button")];
    controls.forEach(control => control.disabled = true);
    setStatus("Waiting for your passkey provider…");
    return action().finally(() => {
        controls.forEach(control => control.disabled = false);
    });
}

async function handleSignIn(form) {
    await disableWhile(form, async () => {
        const options = await postJson("/Account/Passkeys/SignInOptions", {}, form);
        const credentialJson = await requestPasskey(options);
        const result = await postJson("/Account/Passkeys/SignInComplete", {
            credentialJson,
            returnUrl: new FormData(form).get("returnUrl")
        }, form);
        window.location.assign(result.redirectUrl);
    });
}

async function handleBootstrap(form) {
    await disableWhile(form, async () => {
        const fields = new FormData(form);
        const request = {
            token: fields.get("token"),
            tenantName: fields.get("tenantName"),
            userDisplayName: fields.get("userDisplayName")
        };
        const options = await postJson("/Account/Passkeys/BootstrapOptions", request, form);
        request.token = "";
        fields.delete("token");
        form.elements.namedItem("token").value = "";
        const credentialJson = await createPasskey(options);
        const result = await postJson("/Account/Passkeys/BootstrapComplete", {
            credentialJson,
            returnUrl: fields.get("returnUrl")
        }, form);
        form.hidden = true;
        setStatus("Administrator created. Save the recovery codes before continuing.");
        showRecoveryCodes(result);
    });
}

async function handleRecovery(form) {
    await disableWhile(form, async () => {
        const recoveryCode = new FormData(form).get("recoveryCode");
        const options = await postJson(
            "/Account/Passkeys/RecoveryOptions",
            { recoveryCode },
            form);
        const credentialJson = await createPasskey(options);
        const result = await postJson(
            "/Account/Passkeys/RecoveryComplete",
            { credentialJson },
            form);
        form.reset();
        form.hidden = true;
        setStatus("Recovery completed. Existing passkeys and sessions were revoked.");
        showRecoveryCodes(result);
    });
}

async function loadPasskeys(form) {
    const list = document.querySelector("[data-passkey-list]");
    if (!list) return;
    const response = await fetch("/Account/Passkeys/List", {
        credentials: "same-origin",
        cache: "no-store"
    });
    if (!response.ok) throw new Error("identity-request-failed");
    const passkeys = await response.json();
    list.replaceChildren(...passkeys.map(passkey => {
        const item = document.createElement("li");
        item.className = "passkey-item";
        const details = document.createElement("span");
        details.textContent = `${passkey.name} · added ${new Date(passkey.createdAt).toLocaleDateString()}`;
        const revoke = document.createElement("button");
        revoke.type = "button";
        revoke.className = "button danger";
        revoke.textContent = "Remove";
        revoke.setAttribute("aria-label", `Remove passkey ${passkey.name}`);
        revoke.dataset.passkeyRevoke = passkey.credentialId;
        revoke.dataset.passkeyName = passkey.name;
        item.append(details, revoke);
        return item;
    }));
}

async function handleRegistration(form) {
    await disableWhile(form, async () => {
        const deviceName = new FormData(form).get("deviceName");
        const options = await postJson(
            "/Account/Passkeys/RegistrationOptions",
            { deviceName },
            form);
        const credentialJson = await createPasskey(options);
        await postJson("/Account/Passkeys/RegistrationComplete", {
            deviceName,
            credentialJson
        }, form);
        form.reset();
        await loadPasskeys(form);
        setStatus("Passkey added.");
    });
}

async function handleSubmit(event) {
    const form = event.target.closest("form");
    if (!form) return;

    const handler = form.matches("[data-passkey-signin]") ? handleSignIn
        : form.matches("[data-passkey-bootstrap]") ? handleBootstrap
            : form.matches("[data-passkey-recovery]") ? handleRecovery
                : form.matches("[data-passkey-register]") ? handleRegistration
                    : null;
    if (!handler) return;

    event.preventDefault();
    try {
        await handler(form);
    } catch (error) {
        handleFailure(error);
    }
}

async function handleClick(event) {
    const revoke = event.target.closest("[data-passkey-revoke]");
    if (revoke) {
        const form = document.querySelector("[data-passkey-register]");
        if (!form || !window.confirm(`Remove ${revoke.dataset.passkeyName}?`)) return;
        try {
            await postJson(
                "/Account/Passkeys/Revoke",
                { credentialId: revoke.dataset.passkeyRevoke },
                form);
            await loadPasskeys(form);
            setStatus("Passkey removed.");
        } catch (error) {
            handleFailure(error);
        }
        return;
    }

    const continueButton = event.target.closest("[data-recovery-continue]");
    if (continueButton) {
        const section = continueButton.closest("[data-recovery-codes]");
        const confirm = section?.querySelector("[data-recovery-confirm]");
        if (confirm?.checked && section.dataset.redirectUrl) {
            window.location.assign(section.dataset.redirectUrl);
        }
    }
}

function handleChange(event) {
    if (!event.target.matches("[data-recovery-confirm]")) return;
    const section = event.target.closest("[data-recovery-codes]");
    const continueButton = section?.querySelector("[data-recovery-continue]");
    if (continueButton) continueButton.disabled = !event.target.checked;
}

function initializeIdentityPage() {
    const form = document.querySelector("[data-passkey-register]");
    if (form) loadPasskeys(form).catch(handleFailure);
}

let enhancedLoadRegistrationAttempts = 0;
function registerEnhancedLoadHandler() {
    if (window.andrejaIdentityEnhancedLoadSubscribed) return;
    if (globalThis.Blazor?.addEventListener) {
        Blazor.addEventListener("enhancedload", initializeIdentityPage);
        window.andrejaIdentityEnhancedLoadSubscribed = true;
        return;
    }

    enhancedLoadRegistrationAttempts++;
    if (enhancedLoadRegistrationAttempts < 100) {
        window.setTimeout(registerEnhancedLoadHandler, 50);
    }
}

if (!window.andrejaIdentityPasskeysInitialized) {
    window.andrejaIdentityPasskeysInitialized = true;
    document.addEventListener("submit", handleSubmit);
    document.addEventListener("click", handleClick);
    document.addEventListener("change", handleChange);
    registerEnhancedLoadHandler();
}

initializeIdentityPage();
