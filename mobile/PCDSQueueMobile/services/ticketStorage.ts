import AsyncStorage from '@react-native-async-storage/async-storage';

const ACTIVE_TICKET_STORAGE_KEY = 'pcds.activeMobileTicket';

export async function saveActiveTicket<T extends object>(ticket: T): Promise<void> {
  await AsyncStorage.setItem(ACTIVE_TICKET_STORAGE_KEY, JSON.stringify(ticket));
}

export async function getActiveTicket(): Promise<unknown | null> {
  const serialized = await AsyncStorage.getItem(ACTIVE_TICKET_STORAGE_KEY);
  return serialized ? JSON.parse(serialized) as unknown : null;
}

export async function clearActiveTicket(): Promise<void> {
  await AsyncStorage.removeItem(ACTIVE_TICKET_STORAGE_KEY);
}
