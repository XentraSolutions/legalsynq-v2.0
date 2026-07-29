import Reactotron from 'reactotron-react-native';

export function setupReactotron(): void {
  Reactotron.configure({ name: 'LegalSynq Mobile' })
    .useReactNative()
    .connect();
}

export { Reactotron };
