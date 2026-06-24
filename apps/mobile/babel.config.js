module.exports = function (api) {
  api.cache(true);
  const isTest = process.env.NODE_ENV === 'test';
  return {
    presets: ['babel-preset-expo', !isTest && 'nativewind/babel'].filter(Boolean),
    plugins: [
      [
        'module-resolver',
        {
          root: ['./'],
          alias: {
            '@': './src',
          },
        },
      ],
      'react-native-reanimated/plugin',
    ].filter(Boolean),
  };
};
