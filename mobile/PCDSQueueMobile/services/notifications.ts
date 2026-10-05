import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

const QUEUE_NOTIFICATION_CHANNEL = 'queue-calls';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: false,
    shouldShowList: false,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

export async function getQueuePushToken(): Promise<string | null> {
  if (Platform.OS !== 'android' || !Device.isDevice) {
    return null;
  }

  await Notifications.setNotificationChannelAsync(QUEUE_NOTIFICATION_CHANNEL, {
    name: 'Queue Calls',
    importance: Notifications.AndroidImportance.MAX,
    vibrationPattern: [0, 500, 250, 500, 250, 800],
    sound: 'notification.wav',
    lockscreenVisibility: Notifications.AndroidNotificationVisibility.PUBLIC,
    bypassDnd: false,
  });

  let permission = await Notifications.getPermissionsAsync();
  if (permission.status !== 'granted' && permission.canAskAgain) {
    permission = await Notifications.requestPermissionsAsync();
  }
  if (permission.status !== 'granted') {
    return null;
  }

  const projectId =
    Constants.expoConfig?.extra?.eas?.projectId ??
    Constants.easConfig?.projectId;
  if (!projectId) {
    throw new Error('The Expo project ID is missing; push notifications cannot be enabled.');
  }

  const token = await Notifications.getExpoPushTokenAsync({ projectId });
  return token.data;
}
