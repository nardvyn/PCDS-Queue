import Constants from 'expo-constants';
import { isRunningInExpoGo } from 'expo';
import * as Device from 'expo-device';
import { setNotificationChannelAsync } from 'expo-notifications/build/setNotificationChannelAsync';
import {
  AndroidImportance,
  AndroidNotificationVisibility,
} from 'expo-notifications/build/NotificationChannelManager.types';
import { Platform } from 'react-native';

const QUEUE_NOTIFICATION_CHANNEL = 'queue-calls';

export function isExpoGo(): boolean {
  return isRunningInExpoGo();
}

export async function getQueuePushToken(requestPermission = false): Promise<string | null> {
  if (isExpoGo() || Platform.OS !== 'android' || !Device.isDevice) {
    return null;
  }

  await setNotificationChannelAsync(QUEUE_NOTIFICATION_CHANNEL, {
    name: 'Queue Calls',
    importance: AndroidImportance.MAX,
    vibrationPattern: [0, 500, 250, 500, 250, 800],
    sound: 'notification.wav',
    lockscreenVisibility: AndroidNotificationVisibility.PUBLIC,
    bypassDnd: false,
  });

  const { getPermissionsAsync, requestPermissionsAsync } = await import(
    'expo-notifications/build/NotificationPermissions'
  );
  let permission = await getPermissionsAsync();
  if (requestPermission && permission.status !== 'granted' && permission.canAskAgain) {
    permission = await requestPermissionsAsync();
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

  const { getExpoPushTokenAsync } = await import(
    'expo-notifications/build/getExpoPushTokenAsync'
  );
  const token = await getExpoPushTokenAsync({ projectId });
  return token.data;
}
