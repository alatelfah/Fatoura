// Project-wide Babel config so ../clients/shared sources are compiled the same way as the app.
module.exports = function (api) {
  api.cache(true);
  return { presets: ['babel-preset-expo'] };
};
