import { Ionicons } from '@expo/vector-icons';

import { Header } from './Header';

export default { title: 'Shared/Header', component: Header };

export function Variants() {
  return (
    <>
      <Header rightAction={<Ionicons color="#2563eb" name="settings-outline" size={24} />} title="Profile" />
      <Header showBack title="Lien Details" onBack={() => undefined} />
    </>
  );
}
