// Metro config: bundle ../clients/shared (the API client, VAT math and translations shared with the web app).
// Bare imports made from inside ../clients/shared are resolved as if made from this app, so packages such as
// React and React Query come from this app's node_modules and exist exactly once in the bundle
// (the web workspace's copies next to ../clients/shared are never picked up).
const path = require('path');
const { getDefaultConfig } = require('expo/metro-config');

const projectRoot = __dirname;
const sharedRoot = path.resolve(projectRoot, '../clients/shared');

const config = getDefaultConfig(projectRoot);
config.watchFolders = [sharedRoot];
config.resolver.extraNodeModules = { '@fatoura/shared': sharedRoot };
config.resolver.resolveRequest = (context, moduleName, platform) => {
  const fromShared = context.originModulePath.startsWith(sharedRoot + path.sep);
  if (fromShared && !moduleName.startsWith('.') && !path.isAbsolute(moduleName)) {
    return context.resolveRequest({ ...context, originModulePath: path.join(projectRoot, 'package.json') }, moduleName, platform);
  }
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
