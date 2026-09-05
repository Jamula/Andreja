'use strict';

const fs = require('node:fs');
const path = require('node:path');
const {
  PROTECTED_AGENT_FILES,
  PROTECTED_DIRECTORY_PATHS,
  PROTECTED_LITERAL_FILES,
  isProtectedPathIdentity,
} = require('./squad-state-protected-paths');

const MANIFEST_VERSION = 1;

class ManifestError extends Error {}

function isSafeIdentity(identity) {
  return typeof identity === 'string' &&
    identity.length > 0 &&
    !/[\\\u0000-\u001f\u007f]/u.test(identity) &&
    identity.split('/').every(segment =>
      segment.length > 0 && segment !== '.' && segment !== '..');
}

function identityFrom(root, absolutePath) {
  const relative = path.relative(root, absolutePath);
  if (relative === '' ||
      relative === '..' ||
      relative.startsWith(`..${path.sep}`) ||
      path.isAbsolute(relative)) {
    throw new ManifestError('protected path escaped the repository root');
  }
  const identity = relative.split(path.sep).join('/');
  if (!isSafeIdentity(identity)) {
    throw new ManifestError('protected path identity is unsupported');
  }
  return identity;
}

function metadata(identity, stat) {
  return {
    path: identity,
    type: stat.isDirectory() ? 'directory' : 'file',
    size: stat.size.toString(),
    mtimeNs: stat.mtimeNs.toString(),
    ctimeNs: stat.ctimeNs.toString(),
  };
}

function lstat(absolutePath, identity, allowMissing) {
  let stat;
  try {
    stat = fs.lstatSync(absolutePath, { bigint: true });
  } catch (error) {
    if (allowMissing && error && error.code === 'ENOENT') {
      return null;
    }
    throw new ManifestError(`protected path is unreadable: ${identity}`);
  }
  if (stat.isSymbolicLink()) {
    throw new ManifestError(`protected path is a symbolic link or reparse point: ${identity}`);
  }
  return stat;
}

function ensureSafeAncestors(root, identity) {
  const segments = identity.split('/');
  let current = root;
  for (const segment of segments.slice(0, -1)) {
    current = path.join(current, segment);
    const currentIdentity = identityFrom(root, current);
    const stat = lstat(current, currentIdentity, true);
    if (stat === null) {
      return false;
    }
    if (!stat.isDirectory()) {
      throw new ManifestError(`protected path ancestor is not a directory: ${currentIdentity}`);
    }
  }
  return true;
}

function inspectLiteral(root, identity, expectedType) {
  if (!ensureSafeAncestors(root, identity)) {
    return null;
  }
  const absolutePath = path.join(root, ...identity.split('/'));
  const stat = lstat(absolutePath, identity, true);
  if (stat === null) {
    return null;
  }
  if ((expectedType === 'directory' && !stat.isDirectory()) ||
      (expectedType === 'file' && !stat.isFile())) {
    throw new ManifestError(`protected path has an unsupported type: ${identity}`);
  }
  return { absolutePath, stat };
}

function inspectDirectory(root, absoluteDirectory, records) {
  const directoryIdentity = identityFrom(root, absoluteDirectory);
  const stat = lstat(absoluteDirectory, directoryIdentity, false);
  if (!stat.isDirectory()) {
    throw new ManifestError(`protected path has an unsupported type: ${directoryIdentity}`);
  }
  records.push(metadata(directoryIdentity, stat));

  let names;
  try {
    names = fs.readdirSync(absoluteDirectory).sort();
  } catch {
    throw new ManifestError(`protected path is unreadable: ${directoryIdentity}`);
  }
  for (const name of names) {
    const child = path.join(absoluteDirectory, name);
    const childIdentity = identityFrom(root, child);
    const childStat = lstat(child, childIdentity, false);
    if (childStat.isDirectory()) {
      inspectDirectory(root, child, records);
    } else if (childStat.isFile()) {
      records.push(metadata(childIdentity, childStat));
    } else {
      throw new ManifestError(`protected path has an unsupported type: ${childIdentity}`);
    }
  }
}

function inspectAgentFiles(root, records) {
  const agentsIdentity = '.squad/agents';
  const inspected = inspectLiteral(root, agentsIdentity, 'directory');
  if (inspected === null) {
    return;
  }

  let names;
  try {
    names = fs.readdirSync(inspected.absolutePath).sort();
  } catch {
    throw new ManifestError(`protected path is unreadable: ${agentsIdentity}`);
  }
  for (const name of names) {
    const agentDirectory = path.join(inspected.absolutePath, name);
    const agentIdentity = identityFrom(root, agentDirectory);
    const agentStat = lstat(agentDirectory, agentIdentity, false);
    if (!agentStat.isDirectory()) {
      continue;
    }
    for (const file of PROTECTED_AGENT_FILES) {
      const identity = `${agentIdentity}/${file}`;
      const result = inspectLiteral(root, identity, 'file');
      if (result !== null) {
        records.push(metadata(identity, result.stat));
      }
    }
  }
}

function captureRecords(root) {
  const repositoryRoot = path.resolve(root);
  const records = [];

  for (const identity of PROTECTED_DIRECTORY_PATHS) {
    const inspected = inspectLiteral(repositoryRoot, identity, 'directory');
    if (inspected !== null) {
      inspectDirectory(repositoryRoot, inspected.absolutePath, records);
    }
  }
  for (const identity of PROTECTED_LITERAL_FILES) {
    const inspected = inspectLiteral(repositoryRoot, identity, 'file');
    if (inspected !== null) {
      records.push(metadata(identity, inspected.stat));
    }
  }
  inspectAgentFiles(repositoryRoot, records);

  return records.sort((left, right) => left.path.localeCompare(right.path));
}

function externalBaselinePath(root, baselinePath, mustExist) {
  if (!path.isAbsolute(baselinePath)) {
    throw new ManifestError('baseline path must be absolute');
  }
  let repositoryRoot;
  let realParent;
  try {
    repositoryRoot = fs.realpathSync(path.resolve(root));
    realParent = fs.realpathSync(path.dirname(path.resolve(baselinePath)));
  } catch {
    throw new ManifestError('baseline parent directory is unavailable');
  }
  const resolved = path.join(realParent, path.basename(path.resolve(baselinePath)));
  const relative = path.relative(repositoryRoot, resolved);
  if (relative === '' ||
      (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative))) {
    throw new ManifestError('baseline path must be outside the repository');
  }
  if (mustExist) {
    const stat = lstat(resolved, 'baseline', false);
    if (!stat.isFile()) {
      throw new ManifestError('baseline is not a regular file');
    }
  }
  return resolved;
}

function writeBaseline(root, baselinePath) {
  const resolved = externalBaselinePath(root, baselinePath, false);
  const manifest = {
    version: MANIFEST_VERSION,
    records: captureRecords(root),
  };
  let descriptor;
  let ownsBaseline = false;
  try {
    descriptor = fs.openSync(resolved, 'wx', 0o600);
    ownsBaseline = true;
    fs.writeFileSync(descriptor, `${JSON.stringify(manifest)}\n`, 'utf8');
    fs.closeSync(descriptor);
    descriptor = undefined;
  } catch {
    if (descriptor !== undefined) {
      try {
        fs.closeSync(descriptor);
      } catch {}
      descriptor = undefined;
    }
    if (ownsBaseline) {
      try {
        fs.unlinkSync(resolved);
      } catch {}
    }
    throw new ManifestError('baseline could not be created');
  }
}

function validateManifest(value) {
  if (value === null ||
      typeof value !== 'object' ||
      Array.isArray(value) ||
      value.version !== MANIFEST_VERSION ||
      !Array.isArray(value.records)) {
    throw new ManifestError('baseline format is invalid');
  }
  const paths = new Set();
  for (const record of value.records) {
    if (record === null ||
        typeof record !== 'object' ||
        Array.isArray(record) ||
        Object.keys(record).sort().join(',') !== 'ctimeNs,mtimeNs,path,size,type' ||
        !isSafeIdentity(record.path) ||
        !isProtectedPathIdentity(record.path) ||
        paths.has(record.path) ||
        !['directory', 'file'].includes(record.type) ||
        !['size', 'mtimeNs', 'ctimeNs'].every(field =>
          typeof record[field] === 'string' && /^-?\d+$/u.test(record[field]))) {
      throw new ManifestError('baseline format is invalid');
    }
    paths.add(record.path);
  }
  return value.records;
}

function readBaseline(root, baselinePath) {
  const resolved = externalBaselinePath(root, baselinePath, true);
  let value;
  try {
    value = JSON.parse(fs.readFileSync(resolved, 'utf8'));
  } catch {
    throw new ManifestError('baseline could not be read');
  }
  return validateManifest(value);
}

function compareWithBaseline(root, baselinePath) {
  const before = new Map(readBaseline(root, baselinePath).map(record => [record.path, record]));
  const after = new Map(captureRecords(root).map(record => [record.path, record]));
  const identities = new Set([...before.keys(), ...after.keys()]);
  return [...identities]
    .filter(identity => JSON.stringify(before.get(identity)) !== JSON.stringify(after.get(identity)))
    .sort();
}

function parseArguments(argv) {
  if (argv.length !== 3 ||
      !['capture', 'compare'].includes(argv[0]) ||
      argv[1] !== '--baseline' ||
      argv[2].length === 0) {
    throw new ManifestError('usage: squad-state-protected-manifest.js <capture|compare> --baseline <absolute-external-path>');
  }
  return { operation: argv[0], baselinePath: argv[2] };
}

function main(argv = process.argv.slice(2)) {
  const root = path.resolve(__dirname, '..', '..');
  try {
    const { operation, baselinePath } = parseArguments(argv);
    if (operation === 'capture') {
      writeBaseline(root, baselinePath);
      console.log('UNCHANGED');
      return 0;
    }
    const changed = compareWithBaseline(root, baselinePath);
    if (changed.length === 0) {
      console.log('UNCHANGED');
      return 0;
    }
    for (const identity of changed) {
      console.log(`CHANGED ${identity}`);
    }
    return 1;
  } catch (error) {
    console.error(`ERROR: ${error instanceof ManifestError ? error.message : 'manifest operation failed'}`);
    return 2;
  }
}

if (require.main === module) {
  process.exitCode = main();
}

module.exports = {
  ManifestError,
  captureRecords,
  compareWithBaseline,
  main,
  writeBaseline,
};
