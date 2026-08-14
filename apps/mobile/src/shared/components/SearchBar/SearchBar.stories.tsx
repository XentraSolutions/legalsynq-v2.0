import { View } from 'react-native';

import { SearchBar } from './SearchBar';

export default { title: 'Shared/SearchBar', component: SearchBar };

export function Default() {
  return (
    <View className="bg-white p-4">
      <SearchBar placeholder="Search liens" value="Miami" onChangeText={() => undefined} />
    </View>
  );
}
