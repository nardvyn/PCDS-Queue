import AsyncStorage from '@react-native-async-storage/async-storage';
import { Feather } from '@expo/vector-icons';
import { getPermissionsAsync } from 'expo-notifications/build/NotificationPermissions';
import {
  addNotificationResponseReceivedListener,
  getLastNotificationResponseAsync,
} from 'expo-notifications/build/NotificationsEmitter';
import { setNotificationHandler } from 'expo-notifications/build/NotificationsHandler';
import type { NotificationResponse } from 'expo-notifications/build/Notifications.types';
import * as Crypto from 'expo-crypto';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import * as Speech from 'expo-speech';
import { useAudioPlayer } from 'expo-audio';
import Slider from '@react-native-community/slider';
import NetInfo from '@react-native-community/netinfo';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import {
  AppState,
  ActivityIndicator,
  Alert,
  Image,
  KeyboardAvoidingView,
  Linking,
  Modal,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  StatusBar,
  Switch,
  Text,
  Vibration,
  View,
  useWindowDimensions,
} from 'react-native';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import { getQueuePushToken, isExpoGo } from './services/notifications';
import { clearActiveTicket, getActiveTicket, saveActiveTicket } from './services/ticketStorage';
import {
  acknowledgeQueue,
  ApiError,
  cancelMobileQueue,
  checkApiHealth,
  DepartmentQueueStatus,
  ActiveDepartment,
  generateMobileQueue,
  getActiveDepartments,
  getDepartmentQueueStatus,
  getQueueAnnouncementEvents,
  getQueueStatus,
  initializeApiUrl,
  QueueAnnouncementEvent,
  QueueStatus,
  QueueTicket,
  getApiUrl,
  registerQueuePushToken,
  validateQrToken,
  ValidatedQr,
} from './services/api';

const DEVICE_ID_STORAGE_KEY = 'pcds.deviceIdentifier';
const MOBILE_PREFERENCES_STORAGE_KEY = 'pcds.mobilePreferences';
const ANNOUNCEMENT_POLL_INTERVAL_MS = 2000;
const KEEP_AWAKE_TAG = 'pcds-queue-waiting';

type Screen = 'home' | 'scanner' | 'confirm' | 'ticket' | 'settings';
type ActiveTicket = QueueTicket & Omit<QueueStatus, 'department'> & { deviceIdentifier: string };
type VoiceLanguage = 'en-US' | 'fil-PH';
type MobilePreferences = {
  queueAlerts: boolean;
  recallAlerts: boolean;
  vibration: boolean;
  alertSound: boolean;
  ringVolume: number;
  voiceAnnouncement: boolean;
  voiceLanguage: VoiceLanguage;
  voiceVolume: number;
  autoRefresh: boolean;
  refreshIntervalSeconds: number;
  keepScreenAwake: boolean;
  darkMode: boolean;
};

type QueueAlertPresentation = {
  eventType: 'CALLED' | 'RECALLED' | 'TEST';
  queueNumber: string;
  departmentName: string;
  windowName: string | null;
  windowNumber: number | null;
};
type ConnectionType = 'CONNECTED' | 'NO_NETWORK' | 'SERVER_OFFLINE';

const DEFAULT_PREFERENCES: MobilePreferences = {
  queueAlerts: true,
  recallAlerts: true,
  vibration: true,
  alertSound: true,
  ringVolume: 0.8,
  voiceAnnouncement: true,
  voiceLanguage: 'en-US',
  voiceVolume: 0.8,
  autoRefresh: true,
  refreshIntervalSeconds: 5,
  keepScreenAwake: false,
  darkMode: false,
};

const lightColors = {
  // Mirrors the shared desktop resource dictionary.
  ink: '#172033',
  green: '#077F8D',
  greenDark: '#066A76',
  primarySurface: '#077F8D',
  onPrimary: '#FFFFFF',
  onPrimaryMuted: '#C8EBED',
  primaryDivider: 'rgba(255,255,255,0.18)',
  mint: '#E6F4F5',
  lime: '#E3F4F5',
  background: '#F5F7FB',
  surface: '#FFFFFF',
  muted: '#718096',
  faint: '#9CA3AF',
  line: '#E2E8F0',
  amber: '#9A6700',
  amberBg: '#FFF3D7',
  amberBorder: '#E8C76B',
  red: '#DC3545',
  redBg: '#FCEAEA',
  offline: '#DC3545',
  iconOnPrimary: '#FFFFFF',
  scanCaption: '#E3F4F5',
  scanOverlay: 'rgba(6,106,118,0.36)',
  scanBack: 'rgba(0,0,0,0.4)',
  scanButtonText: '#FFFFFF',
  inactiveStatus: '#E9EEF0',
  white: '#FFFFFF',
};

const darkColors: ThemeColors = {
  ink: '#F1F5F9',
  green: '#55BBC3',
  greenDark: '#C6E8EA',
  primarySurface: '#066A76',
  onPrimary: '#FFFFFF',
  onPrimaryMuted: '#C6E8EA',
  primaryDivider: 'rgba(255,255,255,0.24)',
  mint: '#263A55',
  lime: '#C6E8EA',
  background: '#0F1724',
  surface: '#182435',
  muted: '#B2C0D2',
  faint: '#91A0B4',
  line: '#354458',
  amber: '#FFD166',
  amberBg: '#3B321F',
  amberBorder: '#80652D',
  red: '#FF9292',
  redBg: '#3D252B',
  offline: '#FF9292',
  iconOnPrimary: '#FFFFFF',
  scanCaption: '#C6E8EA',
  scanOverlay: 'rgba(4,12,24,0.58)',
  scanBack: 'rgba(0,0,0,0.52)',
  scanButtonText: '#FFFFFF',
  inactiveStatus: '#263445',
  white: '#FFFFFF',
};

type ThemeColors = typeof lightColors;
type AppTheme = { colors: ThemeColors; styles: ReturnType<typeof createStyles> };
const ThemeContext = createContext<AppTheme | null>(null);

export default function App() {
  const [screen, setScreen] = useState<Screen>('home');
  const [showSplash, setShowSplash] = useState(true);
  const [splashStatus, setSplashStatus] = useState('Connecting to queue server...');
  const [cameraPermission, requestCameraPermission] = useCameraPermissions();
  const [apiOnline, setApiOnline] = useState(false);
  const [activeDepartments, setActiveDepartments] = useState<ActiveDepartment[]>([]);
  const [isRefreshingServices, setIsRefreshingServices] = useState(false);
  const [servicesError, setServicesError] = useState('');
  const [deviceIdentifier, setDeviceIdentifier] = useState('');
  const [validatedQr, setValidatedQr] = useState<ValidatedQr | null>(null);
  const [departmentStatus, setDepartmentStatus] = useState<DepartmentQueueStatus | null>(null);
  const [ticket, setTicket] = useState<ActiveTicket | null>(null);
  const [ticketStorageReady, setTicketStorageReady] = useState(false);
  const [latestAnnouncement, setLatestAnnouncement] = useState<QueueAnnouncementEvent | null>(null);
  const [activeAlert, setActiveAlert] = useState<QueueAlertPresentation | null>(null);
  const [preferences, setPreferences] = useState<MobilePreferences>(DEFAULT_PREFERENCES);
  const [preferencesLoaded, setPreferencesLoaded] = useState(false);
  const [preferencesError, setPreferencesError] = useState('');
  const [serverAddress, setServerAddress] = useState('');
  const preferencesRef = useRef(preferences);
  const [isBusy, setIsBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [hasExistingQueueConflict, setHasExistingQueueConflict] = useState(false);
  const [connectionType, setConnectionType] = useState<ConnectionType>('CONNECTED');
  const [showConnectionModal, setShowConnectionModal] = useState(false);
  const [connectionRestored, setConnectionRestored] = useState(false);
  const [notificationsEnabled, setNotificationsEnabled] = useState<boolean | null>(null);
  const [notificationsCanAskAgain, setNotificationsCanAskAgain] = useState(true);
  const runningInExpoGo = isExpoGo();
  const { width } = useWindowDimensions();
  const announcementCursor = useRef<{ queueId: number | null; eventId: number | null }>({
    queueId: null,
    eventId: null,
  });
  const pushTokenQueueId = useRef<number | null>(null);
  const handledNotificationResponse = useRef<string | null>(null);
  const colors = preferences.darkMode ? darkColors : lightColors;
  const styles = useMemo(() => createStyles(colors, width), [colors, width]);
  const appTheme = useMemo(() => ({ colors, styles }), [colors, styles]);
  const announcementPlayer = useAudioPlayer(require('./assets/notification.wav'));
  const activeQueueId = ticket?.queue_id;
  const activeQueueStatus = ticket?.status;

  const checkNotificationPermission = useCallback(async () => {
    if (runningInExpoGo) {
      setNotificationsEnabled(false);
      setNotificationsCanAskAgain(false);
      return;
    }
    try {
      const permission = await getPermissionsAsync();
      setNotificationsEnabled(permission.status === 'granted');
      setNotificationsCanAskAgain(permission.canAskAgain);
    } catch (error) {
      console.warn('Could not check background notification permission.', error);
      setNotificationsEnabled(false);
      setNotificationsCanAskAgain(false);
    }
  }, [runningInExpoGo]);

  const enableNotifications = useCallback(async () => {
    try {
      const token = await getQueuePushToken(true);
      await checkNotificationPermission();
      if (token && ticket && deviceIdentifier) {
        await registerQueuePushToken(ticket.queue_id, deviceIdentifier, token);
        pushTokenQueueId.current = ticket.queue_id;
      }
    } catch (error) {
      console.warn('Could not enable background queue alerts.', error);
      await checkNotificationPermission();
    }
  }, [checkNotificationPermission, deviceIdentifier, ticket]);

  const openNotificationSettings = useCallback(async () => {
    await Linking.openSettings();
  }, []);

  const refreshServices = useCallback(async () => {
    setIsRefreshingServices(true);
    try {
      const departments = await getActiveDepartments();
      setActiveDepartments(departments);
      setServicesError('');
    } catch (error) {
      console.warn('Could not refresh active campus services.', error);
      setServicesError(
        error instanceof Error ? error.message : 'Unable to load campus services. Pull down to retry.',
      );
    } finally {
      setIsRefreshingServices(false);
    }
  }, []);

  const openQueueFromNotification = useCallback(async (
    response: NotificationResponse,
  ) => {
    const responseId = response.notification.request.identifier;
    const queueId = Number(response.notification.request.content.data?.queue_id);
    if (!Number.isSafeInteger(queueId) || queueId <= 0 || ticket?.queue_id !== queueId) {
      return;
    }
    if (handledNotificationResponse.current === responseId) return;
    handledNotificationResponse.current = responseId;

    setScreen('ticket');
    try {
      const status = await getQueueStatus(queueId);
      setTicket((current) => current?.queue_id === queueId
        ? { ...current, ...withoutStatusDepartment(status), deviceIdentifier }
        : current);
    } catch {
      setConnectionType('SERVER_OFFLINE');
      setShowConnectionModal(true);
    }
  }, [deviceIdentifier, ticket?.queue_id]);

  const checkConnection = useCallback(async () => {
    const network = await NetInfo.fetch();
    if (!network.isConnected) {
      setApiOnline(false);
      setConnectionType('NO_NETWORK');
      setShowConnectionModal(true);
      return false;
    }
    try {
      await checkApiHealth();
      setApiOnline(true);
      if (ticket?.queue_id) {
        const latest = await getQueueStatus(ticket.queue_id);
        setTicket((current) => current?.queue_id === ticket.queue_id
          ? { ...current, ...withoutStatusDepartment(latest) }
          : current);
      }
      setConnectionType('CONNECTED');
      setShowConnectionModal((wasVisible) => {
        if (wasVisible) {
          setConnectionRestored(true);
          setTimeout(() => setConnectionRestored(false), 3000);
        }
        return false;
      });
      return true;
    } catch {
      setApiOnline(false);
      setConnectionType('SERVER_OFFLINE');
      setShowConnectionModal(true);
      return false;
    }
  }, [ticket]);

  useEffect(() => {
    let mounted = true;

    const initialize = async () => {
      let restoredTicket: ActiveTicket | null = null;
      try {
        await initializeApiUrl();
        if (mounted) setServerAddress(getApiUrl().replace(/^https?:\/\//i, ''));
        try {
          const savedPreferences = await AsyncStorage.getItem(MOBILE_PREFERENCES_STORAGE_KEY);
          if (mounted) setPreferences(parseMobilePreferences(savedPreferences));
        } catch (error) {
          console.warn('Could not load mobile preferences.', error);
          if (mounted) setPreferencesError('Could not load saved settings. Default settings are in use.');
        } finally {
          if (mounted) setPreferencesLoaded(true);
        }

        let deviceId = await AsyncStorage.getItem(DEVICE_ID_STORAGE_KEY);
        if (!deviceId) {
          deviceId = Crypto.randomUUID();
          await AsyncStorage.setItem(DEVICE_ID_STORAGE_KEY, deviceId);
        }
        if (mounted) setDeviceIdentifier(deviceId);

        const savedTicket = await getActiveTicket();
        if (mounted) {
          if (savedTicket) {
            if (isRestorableTicket(savedTicket)) {
              restoredTicket = { ...savedTicket, deviceIdentifier: deviceId };
              setTicket(restoredTicket);
              setScreen('ticket');
            } else {
              console.warn('Saved queue ticket is invalid and cannot be restored.');
              await clearActiveTicket();
            }
          }
          setTicketStorageReady(true);
        }
      } catch {
        if (mounted) {
          console.warn('Could not restore mobile app data.');
          setApiOnline(false);
          setErrorMessage('Could not restore saved app data. Your queue ticket may not be available.');
        }
      }

      try {
        await checkApiHealth();
        if (!mounted) return;
        setApiOnline(true);
        setConnectionType('CONNECTED');
        setSplashStatus('Queue server connected');
        if (restoredTicket) {
          const status = await getQueueStatus(restoredTicket.queue_id);
          if (!mounted) return;
          const refreshedTicket: ActiveTicket = {
            ...restoredTicket,
            ...withoutStatusDepartment(status),
            deviceIdentifier: restoredTicket.deviceIdentifier,
          };
          await saveActiveTicket(refreshedTicket);
          setTicket(refreshedTicket);
        }
      } catch (error) {
        if (mounted) {
          console.warn('Could not refresh the saved queue ticket from the server.', error);
          setApiOnline(false);
          setConnectionType('SERVER_OFFLINE');
          if (restoredTicket) setShowConnectionModal(true);
          else setSplashStatus('Starting PCDS Queue...');
        }
      }
    };

    void initialize();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener((network) => {
      if (!network.isConnected) {
        setApiOnline(false);
        setConnectionType('NO_NETWORK');
        setShowConnectionModal(true);
      } else {
        void checkConnection();
      }
    });
    return unsubscribe;
  }, [checkConnection]);

  useEffect(() => {
    if (screen !== 'ticket' && screen !== 'settings') return;
    const timer = setTimeout(() => void checkNotificationPermission(), 0);
    return () => clearTimeout(timer);
  }, [checkNotificationPermission, screen]);

  useEffect(() => {
    if (screen !== 'home' || !apiOnline) return;
    const timer = setTimeout(() => void refreshServices(), 0);
    return () => clearTimeout(timer);
  }, [apiOnline, refreshServices, screen]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active' && (screen === 'ticket' || screen === 'settings')) {
        void checkNotificationPermission();
      }
      if (state === 'active' && screen === 'home' && apiOnline) {
        void refreshServices();
      }
    });
    return () => subscription.remove();
  }, [apiOnline, checkNotificationPermission, refreshServices, screen]);

  useEffect(() => {
    const timer = setTimeout(() => setShowSplash(false), 2500);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    if (runningInExpoGo) return;

    setNotificationHandler({
      handleNotification: async () => ({
        shouldShowBanner: false,
        shouldShowList: false,
        shouldPlaySound: false,
        shouldSetBadge: false,
      }),
    });
    const subscription = addNotificationResponseReceivedListener((response) => {
      void openQueueFromNotification(response);
    });
    void getLastNotificationResponseAsync()
      .then((response) => {
        if (response) void openQueueFromNotification(response);
      })
      .catch((error: unknown) => {
        console.warn('Could not read the notification that opened the app.', error);
      });

    return () => {
      subscription.remove();
    };
  }, [openQueueFromNotification, runningInExpoGo]);

  useEffect(() => {
    if (!ticket) return;
    void saveActiveTicket(ticket).catch((error: unknown) => {
      console.warn('Could not save the active queue ticket.', error);
      setErrorMessage('Could not save your ticket on this device. Keep the app installed and open.');
    });
  }, [ticket]);

  useEffect(() => {
    if (!preferencesLoaded) return;
    const timer = setTimeout(() => {
      void AsyncStorage.setItem(MOBILE_PREFERENCES_STORAGE_KEY, JSON.stringify(preferences))
        .then(() => setPreferencesError(''))
        .catch((error: unknown) => {
          console.warn('Could not save mobile preferences.', error);
          setPreferencesError('Could not save settings. Check your device storage and try again.');
        });
    }, 250);
    return () => clearTimeout(timer);
  }, [preferences, preferencesLoaded]);

  useEffect(() => {
    preferencesRef.current = preferences;
  }, [preferences]);

  useEffect(() => {
    if (!activeQueueId || !deviceIdentifier ||
        !['WAITING', 'CALLED', 'SERVING'].includes(activeQueueStatus ?? '') ||
        pushTokenQueueId.current === activeQueueId) {
      return;
    }

    let mounted = true;
    pushTokenQueueId.current = activeQueueId;
    void getQueuePushToken()
      .then(async (pushToken) => {
        if (!mounted || !pushToken) return;
        await registerQueuePushToken(activeQueueId, deviceIdentifier, pushToken);
      })
      .catch((error: unknown) => {
        console.warn('Could not register background queue notifications.', error);
      });

    return () => {
      mounted = false;
    };
  }, [activeQueueId, activeQueueStatus, deviceIdentifier]);

  useEffect(() => {
    if (!preferences.autoRefresh || !activeQueueId || !deviceIdentifier || !activeQueueStatus ||
        !['WAITING', 'CALLED', 'SERVING'].includes(activeQueueStatus)) {
      return;
    }

    let mounted = true;
    let refreshing = false;
    const refreshStatus = async () => {
      if (refreshing) return;
      refreshing = true;

      try {
        const status = await getQueueStatus(activeQueueId);
        if (!mounted) return;

        setApiOnline(true);
        setTicket((current) =>
          current?.queue_id === activeQueueId
            ? { ...current, ...withoutStatusDepartment(status), deviceIdentifier }
            : current,
        );

      } catch {
        if (mounted) {
          setApiOnline(false);
          setConnectionType('SERVER_OFFLINE');
          setShowConnectionModal(true);
        }
      } finally {
        refreshing = false;
      }
    };

    void refreshStatus();
    const timer = setInterval(
      () => void refreshStatus(),
      preferences.refreshIntervalSeconds * 1000,
    );
    return () => {
      mounted = false;
      clearInterval(timer);
    };
  }, [activeQueueId, activeQueueStatus, deviceIdentifier, preferences.autoRefresh, preferences.refreshIntervalSeconds]);

  useEffect(() => {
    if (!activeQueueId || !deviceIdentifier ||
        !['WAITING', 'CALLED', 'SERVING'].includes(activeQueueStatus ?? '')) return;

    let mounted = true;
    let refreshing = false;
    if (announcementCursor.current.queueId !== activeQueueId) {
      announcementCursor.current = { queueId: activeQueueId, eventId: null };
      setLatestAnnouncement(null);
    }

    const refreshAnnouncements = async () => {
      if (refreshing) return;
      refreshing = true;
      try {
        const cursor = announcementCursor.current;
        const response = await getQueueAnnouncementEvents(
          activeQueueId,
          deviceIdentifier,
          cursor.eventId ?? 0,
        );
        if (!mounted) return;
        if (cursor.eventId === null) {
          cursor.eventId = response.latest_event_id;
          return;
        }
        for (const event of response.events) {
          if (event.queue_id !== activeQueueId || event.event_id <= cursor.eventId) continue;
          cursor.eventId = event.event_id;
          if (!mounted || !preferencesRef.current.queueAlerts) continue;
          setLatestAnnouncement(event);
          setTicket((current) =>
            current?.queue_id === activeQueueId
              ? {
                  ...current,
                  status: 'CALLED',
                  window_name: event.window_name ?? current.window_name,
                  window_number: event.window_number ?? current.window_number,
                  current_serving: event.queue_number,
                }
              : current,
          );
          if (event.event_type !== 'RECALLED' || preferencesRef.current.recallAlerts) {
            setActiveAlert({
              eventType: event.event_type,
              queueNumber: event.queue_number,
              departmentName: event.department_name,
              windowName: event.window_name,
              windowNumber: event.window_number,
            });
          }
        }
        cursor.eventId = Math.max(cursor.eventId, response.next_after_event_id);
      } catch {
        if (mounted) {
          setApiOnline(false);
          setConnectionType('SERVER_OFFLINE');
          setShowConnectionModal(true);
        }
      } finally {
        refreshing = false;
      }
    };

    void refreshAnnouncements();
    const timer = setInterval(() => void refreshAnnouncements(), ANNOUNCEMENT_POLL_INTERVAL_MS);
    return () => {
      mounted = false;
      clearInterval(timer);
    };
  }, [activeQueueId, activeQueueStatus, announcementPlayer, deviceIdentifier]);

  // Expo Audio playback is controlled through its imperative AudioPlayer API.
  // eslint-disable-next-line react-hooks/immutability
  useEffect(() => {
    if (!activeAlert) return;
    const alertPreferences = preferencesRef.current;
    if (!alertPreferences.queueAlerts) return;

    let alertActive = true;
    let voiceTimer: ReturnType<typeof setTimeout> | undefined;

    if (alertPreferences.alertSound) {
      // eslint-disable-next-line react-hooks/immutability
      announcementPlayer.loop = true;
      announcementPlayer.volume = alertPreferences.ringVolume;
      void announcementPlayer.seekTo(0)
        .then(() => {
          if (alertActive) announcementPlayer.play();
        })
        .catch((error: unknown) => {
          console.warn('Could not play the queue alert sound.', error);
        });
    }

    if (alertPreferences.vibration) {
      try {
        Vibration.vibrate([0, 700, 400], true);
      } catch (error) {
        console.warn('Could not vibrate for the queue alert.', error);
      }
    }

    if (alertPreferences.voiceAnnouncement) {
      const message = activeAlert.eventType === 'TEST'
        ? 'This is a PCDS Queue test alert.'
        : createQueueAnnouncementMessage(activeAlert);
      voiceTimer = setTimeout(() => {
        if (!alertActive) return;
        try {
          if (alertPreferences.alertSound) announcementPlayer.pause();
          Speech.speak(message, {
            rate: 0.92,
            language: alertPreferences.voiceLanguage,
            volume: alertPreferences.voiceVolume,
            onDone: () => {
              if (alertActive && alertPreferences.alertSound) announcementPlayer.play();
            },
            onError: (error) => {
              console.warn('Could not speak the queue alert.', error);
              if (alertActive && alertPreferences.alertSound) announcementPlayer.play();
            },
          });
        } catch (error) {
          console.warn('Could not speak the queue alert.', error);
          if (alertActive && alertPreferences.alertSound) announcementPlayer.play();
        }
      }, alertPreferences.alertSound ? 700 : 0);
    }

    return () => {
      alertActive = false;
      if (voiceTimer) clearTimeout(voiceTimer);
      announcementPlayer.loop = false;
      try {
        announcementPlayer.pause();
        void announcementPlayer.seekTo(0).catch((error: unknown) => {
          console.warn('Could not reset the queue alert sound.', error);
        });
      } catch (error) {
        console.warn('Could not stop the queue alert sound.', error);
      }
      try {
        Vibration.cancel();
      } catch (error) {
        console.warn('Could not stop queue alert vibration.', error);
      }
      Speech.stop();
    };
  }, [activeAlert, announcementPlayer]);

  useEffect(() => {
    const shouldKeepAwake = preferences.keepScreenAwake &&
      ticket?.status === 'WAITING';
    if (!shouldKeepAwake) {
      void deactivateKeepAwake(KEEP_AWAKE_TAG).catch((error: unknown) => {
        console.warn('Could not release the screen awake lock.', error);
      });
      return;
    }

    let mounted = true;
    void activateKeepAwakeAsync(KEEP_AWAKE_TAG)
      .then(() => {
        if (!mounted) return deactivateKeepAwake(KEEP_AWAKE_TAG);
      })
      .catch((error: unknown) => {
        console.warn('Could not keep the screen awake.', error);
        if (mounted) setPreferencesError('Could not keep the screen awake on this device.');
      });
    return () => {
      mounted = false;
      void deactivateKeepAwake(KEEP_AWAKE_TAG).catch((error: unknown) => {
        console.warn('Could not release the screen awake lock.', error);
      });
    };
  }, [preferences.keepScreenAwake, ticket?.status]);

  const joinQueue = useCallback(async (qr: ValidatedQr): Promise<boolean> => {
    if (!ticketStorageReady) {
      setErrorMessage('Your saved ticket is still being restored. Please try again in a moment.');
      return false;
    }
    if (ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status)) {
      setScreen('ticket');
      return true;
    }
    if (!deviceIdentifier) {
      setErrorMessage('Device setup is incomplete. Please restart the app and try again.');
      return false;
    }
    setIsBusy(true);
    setErrorMessage('');
    setHasExistingQueueConflict(false);
    let pushToken: string | null = null;
    try {
      try {
        pushToken = await getQueuePushToken();
      } catch (error) {
        console.warn('Background notifications could not be prepared; queue entry will continue.', error);
      }

      const generated = await generateMobileQueue(
        qr.department.department_id,
        deviceIdentifier,
        qr.token,
        pushToken ?? undefined,
      );
      pushTokenQueueId.current = generated.queue_id;
      const activeTicket: ActiveTicket = {
        ...generated,
        estimated_wait_minutes: 0,
        current_serving: null,
        window_number: null,
        window_name: null,
        called_at: null,
        grace_period_seconds: 0,
        grace_remaining_seconds: 0,
        call_countdown_enabled: false,
        customer_acknowledgement_enabled: false,
        can_no_show: false,
        customer_acknowledged: false,
        deviceIdentifier,
      };
      try {
        await saveActiveTicket(activeTicket);
      } catch (storageError) {
        console.warn('Could not save the newly created queue ticket.', storageError);
        setErrorMessage('Your ticket was created, but could not be saved on this device.');
      }
      announcementCursor.current = { queueId: generated.queue_id, eventId: 0 };
      setTicket(activeTicket);
      setApiOnline(true);
      setScreen('ticket');
      setValidatedQr(null);
      setDepartmentStatus(null);
      try {
        const status = await getQueueStatus(generated.queue_id);
        const refreshedTicket: ActiveTicket = {
          ...activeTicket,
          ...withoutStatusDepartment(status),
          deviceIdentifier,
        };
        await saveActiveTicket(refreshedTicket);
        setTicket(refreshedTicket);
      } catch (statusError) {
        console.warn('Could not refresh the newly created queue ticket.', statusError);
        setApiOnline(false);
        setConnectionType('SERVER_OFFLINE');
        setShowConnectionModal(true);
      }
      return true;
    } catch (error) {
      const apiError = error instanceof ApiError ? error : null;
      const existingQueue = (apiError?.payload as { queue?: { queue_id?: number } } | null)?.queue;
      if (apiError?.statusCode === 409 && existingQueue?.queue_id) {
        setHasExistingQueueConflict(true);
        try {
          let pushTokenRegistered = false;
          if (pushToken) {
            try {
              await registerQueuePushToken(existingQueue.queue_id, deviceIdentifier, pushToken);
              pushTokenRegistered = true;
            } catch (registrationError) {
              console.warn('Could not register background alerts for the active ticket.', registrationError);
            }
          }
          pushTokenQueueId.current = pushToken && !pushTokenRegistered
            ? null
            : existingQueue.queue_id;
          const status = await getQueueStatus(existingQueue.queue_id);
          const resumed: ActiveTicket = {
            ...withoutStatusDepartment(status),
            sequence_number: 0,
            department: {
              department_id: qr.department.department_id,
              department_name: qr.department.department_name,
              queue_prefix: qr.department.queue_prefix,
            },
            source: 'MOBILE',
            deviceIdentifier,
          };
          try {
            await saveActiveTicket(resumed);
          } catch (storageError) {
            console.warn('Could not save the resumed queue ticket.', storageError);
            setErrorMessage('Your active ticket was restored, but could not be saved on this device.');
          }
          setTicket(resumed);
          setScreen('ticket');
          setValidatedQr(null);
          setDepartmentStatus(null);
          setHasExistingQueueConflict(false);
          return true;
        } catch {
          // Show the original conflict if the active ticket cannot be resumed.
        }
      }
      const qrReason = (apiError?.payload as { reason?: string } | null)?.reason;
      if (qrReason === 'EXPIRED' || qrReason === 'INACTIVE' || qrReason === 'INVALID') {
        setValidatedQr(null);
        setDepartmentStatus(null);
        setScreen('home');
        if (qrReason === 'EXPIRED') {
          Alert.alert(
            'QR Code Expired',
            'This QR code has expired. Please scan the latest PCDS Queue QR code.',
          );
        } else if (qrReason === 'INACTIVE') {
          Alert.alert('QR Code Inactive', 'This QR code is no longer active.');
        } else {
          Alert.alert(
            'Invalid QR Code',
            'This QR code is not recognized by the PCDS Queue System.',
          );
        }
        return false;
      }
      if (!apiError || apiError.statusCode >= 500) {
        setApiOnline(false);
        setConnectionType('SERVER_OFFLINE');
        setShowConnectionModal(true);
        return false;
      }
      setErrorMessage(getErrorMessage(error));
      return false;
    } finally {
      setIsBusy(false);
    }
  }, [deviceIdentifier, ticket, ticketStorageReady]);

  const handleQrScanned = useCallback(async (token: string): Promise<boolean> => {
    setIsBusy(true);
    setErrorMessage('');
    try {
      const result = await validateQrToken(token.trim());
      setValidatedQr(result);
      try {
        setDepartmentStatus(await getDepartmentQueueStatus(result.department.department_id));
      } catch {
        setDepartmentStatus(null);
      }
      setApiOnline(true);
      setScreen('confirm');
      return true;
    } catch (error) {
      if (!(error instanceof ApiError) || error.statusCode >= 500) {
        setApiOnline(false);
        setConnectionType('SERVER_OFFLINE');
        setShowConnectionModal(true);
        return false;
      }
      const reason = (error.payload as { reason?: string } | null)?.reason;
      if (reason === 'EXPIRED') {
        setScreen('home');
        Alert.alert(
          'QR Code Expired',
          'This QR code has expired. Please scan the latest PCDS Queue QR code.',
        );
        return false;
      }
      if (reason === 'INACTIVE') {
        setScreen('home');
        Alert.alert('QR Code Inactive', 'This QR code is no longer active.');
        return false;
      }
      if (reason === 'INVALID') {
        setScreen('home');
        Alert.alert(
          'Invalid QR Code',
          'This QR code is not recognized by the PCDS Queue System.',
        );
        return false;
      }
      setErrorMessage(getErrorMessage(error));
      return false;
    } finally {
      setIsBusy(false);
    }
  }, []);

  const openScanner = async () => {
    setErrorMessage('');
    if (!ticketStorageReady) return;
    if (ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status)) {
      setScreen('ticket');
      return;
    }
    if (!cameraPermission?.granted) {
      const permission = await requestCameraPermission();
      if (!permission.granted) {
        setErrorMessage('Camera permission is needed to scan a department QR code.');
        return;
      }
    }
    setScreen('scanner');
  };

  const cancelTicket = () => {
    if (!ticket) return;
    Alert.alert(
      'Cancel queue number?',
      `${ticket.queue_number} will be removed from the waiting line.`,
      [
        { text: 'Keep ticket', style: 'cancel' },
        {
          text: 'Cancel ticket',
          style: 'destructive',
          onPress: () => void submitCancellation(),
        },
      ],
    );
  };

  const submitCancellation = async () => {
    if (!ticket || !deviceIdentifier) return;
    setIsBusy(true);
    setErrorMessage('');
    try {
      await cancelMobileQueue(ticket.queue_id, deviceIdentifier);
      setTicket({ ...ticket, status: 'CANCELLED', people_ahead: 0 });
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsBusy(false);
    }
  };

  const handleTicketDone = async () => {
    setIsBusy(true);
    setErrorMessage('');
    try {
      await clearActiveTicket();
      setTicket(null);
      setScreen('home');
    } catch (error) {
      console.warn('Could not clear the completed queue ticket.', error);
      setErrorMessage('Could not clear the saved ticket. Please try again.');
    } finally {
      setIsBusy(false);
    }
  };

  const handleOnMyWay = async () => {
    if (!ticket) return;
    setIsBusy(true);
    setErrorMessage('');
    try {
      await acknowledgeQueue(ticket.queue_id);
      setTicket((current) => current ? { ...current, customer_acknowledged: true } : null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsBusy(false);
    }
  };

  const updatePreferences = (updates: Partial<MobilePreferences>) => {
    setPreferences((current) => ({ ...current, ...updates }));
    setPreferencesError('');
  };

  const playTestAlert = () => {
    setActiveAlert({
      eventType: 'TEST',
      queueNumber: '',
      departmentName: '',
      windowName: null,
      windowNumber: null,
    });
  };

  const returnHomeFromScanner = useCallback(() => {
    setScreen('home');
    requestAnimationFrame(() => {
      const backgroundColor = preferences.darkMode ? colors.background : colors.white;
      StatusBar.setHidden(false, 'fade');
      StatusBar.setBarStyle(preferences.darkMode ? 'light-content' : 'dark-content', true);
      if (Platform.OS === 'android') {
        StatusBar.setBackgroundColor(backgroundColor, true);
      }
    });
  }, [colors.background, colors.white, preferences.darkMode]);

  const appContent = showSplash ? (
    <SplashScreen status={splashStatus} />
  ) : screen === 'scanner' ? (
    <QrScannerScreen
      cameraPermissionGranted={!!cameraPermission?.granted}
      onRequestCameraPermission={() => void openScanner()}
      onBack={returnHomeFromScanner}
      onBarcodeFound={handleQrScanned}
      errorMessage={errorMessage}
      isBusy={isBusy}
    />
  ) : (
    <SafeAreaView style={styles.safeArea}>

      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          refreshControl={screen === 'home' ? (
            <RefreshControl
              refreshing={isRefreshingServices}
              onRefresh={() => void refreshServices()}
              tintColor={colors.green}
              colors={[colors.green]}
            />
          ) : undefined}
        >
          <View style={styles.header}>
            <View style={styles.brandRow}>
              <View style={styles.brandMark}>
                <Image source={require('./assets/pcds-logo.png')} style={styles.brandLogo} resizeMode="contain" />
              </View>
              <View>
                <Text style={styles.brandName}>PCDS QUEUE</Text>
                <Text style={styles.brandCaption}>CAMPUS SERVICES</Text>
              </View>
            </View>
          </View>

          <View style={styles.connectionRow}>
            <View style={[styles.connectionDot, { backgroundColor: apiOnline ? colors.green : colors.faint }]} />
            <Text style={styles.connectionText}>{apiOnline ? 'QUEUE SERVER CONNECTED' : 'CONNECTING TO QUEUE SERVER'}</Text>
          </View>

          {screen === 'settings' && (
            <SettingsScreen
              preferences={preferences}
              onChange={updatePreferences}
              onTestAlert={playTestAlert}
              apiOnline={apiOnline}
              serverAddress={serverAddress}
              errorMessage={preferencesError}
              notificationsEnabled={notificationsEnabled === true}
              notificationsCanAskAgain={notificationsCanAskAgain}
              runningInExpoGo={runningInExpoGo}
              onEnableNotifications={enableNotifications}
              onOpenNotificationSettings={openNotificationSettings}
            />
          )}

          {screen === 'home' && (
            <>
              <View style={styles.hero}>
                <Text style={styles.eyebrow}>WELCOME TO PCDS</Text>
                <Text style={styles.heroTitle}>Join a Queue</Text>
                <Text style={styles.heroCopy}>Scan a campus service QR code to get your queue number without waiting in line.</Text>
              </View>

              {!ticketStorageReady && !!errorMessage && (
                <InlineNotice message={errorMessage} tone="error" />
              )}

              {ticket && (
                <ActiveTicketCard
                  ticket={ticket}
                  announcement={preferences.queueAlerts ? latestAnnouncement : null}
                  onPress={() => setScreen('ticket')}
                />
              )}

              <Pressable
                style={[
                  styles.scanButton,
                  (!ticketStorageReady || (ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status))) &&
                    styles.disabledButton,
                ]}
                onPress={() => void openScanner()}
                disabled={!ticketStorageReady || (!!ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status))}
              >
                <View style={styles.scanIcon}>
                  <Feather name="maximize" size={22} color="white" />
                </View>
                <View style={styles.scanButtonText}>
                  <Text style={styles.scanButtonTitle}>
                    {!ticketStorageReady
                      ? 'Restoring ticket…'
                      : ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status)
                        ? 'Ticket active'
                        : 'SCAN DEPARTMENT QR'}
                  </Text>
                  <Text style={styles.scanButtonCaption}>
                    {!ticketStorageReady
                      ? 'Checking this device for a saved queue ticket'
                      : ticket && ['WAITING', 'CALLED', 'SERVING'].includes(ticket.status)
                        ? 'Open your current queue ticket above'
                        : 'Department is detected automatically'}
                  </Text>
                </View>
                <Feather name="arrow-up-right" size={20} color="white" />
              </Pressable>

              <View style={styles.serviceNote}>
                <View style={styles.noteRule} />
                <Text style={styles.noteLabel}>SERVICES</Text>
                <Text style={styles.noteText}>
                  {activeDepartments.length > 0
                    ? activeDepartments.map((department) => department.department_name).join(' · ')
                    : isRefreshingServices
                      ? 'Loading services…'
                      : 'No active services available'}
                </Text>
                {!!servicesError && (
                  <Text style={styles.serviceError}>
                    {activeDepartments.length > 0
                      ? 'Could not refresh services. Pull down to try again.'
                      : 'Could not load services. Pull down to try again.'}
                  </Text>
                )}
              </View>
            </>
          )}

          {screen === 'confirm' && validatedQr && (
            <View style={styles.screenSection}>
              <Pressable style={styles.backRow} onPress={() => { setScreen('home'); setValidatedQr(null); setDepartmentStatus(null); }}>
                <Feather name="arrow-left" size={18} color={colors.green} />
                <Text style={styles.backText}>Back</Text>
              </Pressable>
              <Text style={styles.eyebrow}>QR VERIFIED</Text>
              <Text style={styles.sectionTitle}>Confirm your service</Text>
              <View style={styles.departmentCard}>
                <View style={styles.departmentPrefix}><Text style={styles.prefixText}>{validatedQr.department.queue_prefix}</Text></View>
                <View style={styles.flex}>
                  <Text style={styles.departmentName}>{validatedQr.department.department_name}</Text>
                  <Text style={styles.departmentMeta}>{validatedQr.department.queue_is_open ? 'Queue is open' : 'Queue is closed'}</Text>
                </View>
                <Feather name={validatedQr.department.queue_is_open ? 'check-circle' : 'slash'} size={21} color={validatedQr.department.queue_is_open ? colors.green : colors.red} />
              </View>
              <Text style={styles.helperText}>
                {validatedQr.department.queue_is_open && validatedQr.mobile_queue_enabled
                  ? `This QR belongs to ${validatedQr.department.department_name}.`
                  : `Queue entry is unavailable for ${validatedQr.department.department_name}.`}
              </Text>
              <View style={styles.queueInfoGrid}>
                <QueueInfo label="CURRENT SERVING" value={departmentStatus?.serving[0]?.queue_number ?? '—'} />
                <QueueInfo label="WAITING" value={departmentStatus?.waiting_count?.toString() ?? '—'} />
                <QueueInfo label="ACTIVE WINDOWS" value={departmentStatus?.active_window_count?.toString() ?? '—'} />
              </View>
              {!validatedQr.department.queue_is_open && <InlineNotice message="This queue is currently closed. Please try again when it opens." tone="warning" />}
              {!validatedQr.mobile_queue_enabled && <InlineNotice message="Mobile queue entry is currently disabled by the administrator." tone="warning" />}
              {!!errorMessage && <InlineNotice message={errorMessage} tone="error" />}
              <ActionButton
                title={hasExistingQueueConflict ? 'ACTIVE QUEUE FOUND' : isBusy ? 'Joining queue…' : 'JOIN QUEUE'}
                onPress={() => void joinQueue(validatedQr)}
                disabled={isBusy || hasExistingQueueConflict || !validatedQr.department.queue_is_open || !validatedQr.mobile_queue_enabled}
                icon="arrow-right"
              />
            </View>
          )}

          {screen === 'ticket' && ticket && (
            <View style={styles.screenSection}>
              <Pressable style={styles.backRow} onPress={() => setScreen('home')}>
                <Feather name="arrow-left" size={18} color={colors.green} />
                <Text style={styles.backText}>Home</Text>
              </Pressable>
              <View style={styles.ticketHeading}>
                <View>
                  <Text style={styles.eyebrow}>YOUR QUEUE TICKET</Text>
                  <Text style={styles.sectionTitle}>{ticket.department.department_name}</Text>
                </View>
                <StatusPill status={ticket.status} />
              </View>
              {(runningInExpoGo || notificationsEnabled === false) && (
                <View style={styles.notificationWarning}>
                  <View style={styles.flex}>
                    <Text style={styles.notificationWarningTitle}>
                      {runningInExpoGo ? 'Background alerts need a development build' : 'Background alerts are off'}
                    </Text>
                    <Text style={styles.notificationWarningText}>
                      {runningInExpoGo
                        ? 'Android push notifications are not available in Expo Go. Use a development build to receive queue calls in the background.'
                        : 'Enable notifications so you do not miss your queue call.'}
                    </Text>
                  </View>
                  {!runningInExpoGo && (
                    <Pressable
                      style={styles.notificationEnableButton}
                      onPress={() => void (notificationsCanAskAgain ? enableNotifications() : openNotificationSettings())}
                      accessibilityRole="button"
                      accessibilityLabel={notificationsCanAskAgain ? 'Enable queue notifications' : 'Open notification settings'}
                    >
                      <Text style={styles.notificationEnableButtonText}>
                        {notificationsCanAskAgain ? 'ENABLE' : 'OPEN SETTINGS'}
                      </Text>
                    </Pressable>
                  )}
                </View>
              )}

              {preferences.queueAlerts && latestAnnouncement?.queue_id === ticket.queue_id &&
                ['CALLED', 'SERVING'].includes(ticket.status) && (
                <View style={styles.announcementCard} accessibilityLiveRegion="assertive">
                  <View style={styles.announcementIcon}>
                    <Feather name="bell" size={20} color={colors.amber} />
                  </View>
                  <View style={styles.flex}>
                    <Text style={styles.announcementEyebrow}>
                      {latestAnnouncement.event_type === 'RECALLED' ? 'QUEUE RECALL' : 'QUEUE CALL'}
                    </Text>
                    <Text style={styles.announcementNumber}>{latestAnnouncement.queue_number}</Text>
                    <Text style={styles.announcementText}>Please proceed to</Text>
                    <Text style={styles.announcementDestination}>
                      {latestAnnouncement.department_name.toUpperCase()} •{' '}
                      {(latestAnnouncement.window_number
                        ? `Window ${latestAnnouncement.window_number}`
                        : latestAnnouncement.window_name ?? 'Service window').toUpperCase()}
                    </Text>
                    <Text style={styles.announcementFooter}>
                      {latestAnnouncement.event_type === 'RECALLED'
                        ? 'Your number has been recalled.'
                        : ticket.status === 'SERVING'
                          ? 'Status: NOW SERVING'
                          : 'Your number is being called.'}
                    </Text>
                  </View>
                </View>
              )}
              {ticket.status === 'CALLED' && latestAnnouncement?.queue_id !== ticket.queue_id && (
                <InlineNotice
                  message={`Your number is being called. Please proceed to ${ticket.window_name ?? 'the service window'}.`}
                  tone="success"
                />
              )}
              {ticket.status === 'SERVING' && latestAnnouncement?.queue_id !== ticket.queue_id && (
                <InlineNotice
                  message={`You are now being served${ticket.window_name ? ` at ${ticket.window_name}` : ''}.`}
                  tone="success"
                />
              )}

              {['CALLED', 'SERVING'].includes(ticket.status) &&
                (ticket.call_countdown_enabled || ticket.customer_acknowledgement_enabled) && (
                <View style={styles.callGraceCard}>
                  {ticket.call_countdown_enabled && <>
                    <Text style={styles.callGraceLabel}>TIME TO PROCEED</Text>
                    <Text style={styles.callGraceTimer}>{formatGraceTime(ticket.grace_remaining_seconds)}</Text>
                  </>}
                  {ticket.customer_acknowledgement_enabled && (ticket.customer_acknowledged ? (
                    <Text style={styles.callGraceAcknowledged}>✓ STAFF NOTIFIED — YOU&apos;RE ON YOUR WAY</Text>
                  ) : (
                    <Pressable style={styles.callGraceButton} onPress={() => void handleOnMyWay()} disabled={isBusy}>
                      <Text style={styles.callGraceButtonText}>{isBusy ? 'NOTIFYING…' : "I'M ON MY WAY"}</Text>
                    </Pressable>
                  ))}
                </View>
              )}
              <View style={styles.ticketCard}>
                <Text style={styles.ticketLabel}>QUEUE NUMBER</Text>
                <Text style={styles.ticketNumber}>{ticket.queue_number}</Text>
                <View style={styles.ticketDivider} />
                <View style={styles.ticketStats}>
                  <View style={styles.ticketStat}>
                    <Text style={styles.statValue}>{ticket.status === 'WAITING' ? ticket.people_ahead : '—'}</Text>
                    <Text style={styles.statLabel}>PEOPLE AHEAD</Text>
                  </View>
                  <View style={styles.statDivider} />
                  <View style={styles.ticketStat}>
                    <Text style={styles.statValue}>{ticket.status === 'WAITING' ? `~${ticket.estimated_wait_minutes}m` : '—'}</Text>
                    <Text style={styles.statLabel}>EST. WAIT</Text>
                  </View>
                </View>
              </View>

              <View style={styles.currentServingCard}>
                <View style={styles.servingIcon}><Feather name="volume-2" size={18} color={colors.green} /></View>
                <View style={styles.flex}>
                  <Text style={styles.currentServingLabel}>NOW SERVING</Text>
                  <Text style={styles.currentServingNumber}>{ticket.current_serving ?? '—'}</Text>
                  {!!ticket.window_name && <Text style={styles.windowText}>{ticket.window_name}</Text>}
                </View>
                <View style={styles.liveStatus}>
                  <View style={[styles.liveStatusDot, { backgroundColor: apiOnline ? colors.green : colors.faint }]} />
                  <Text style={styles.liveStatusText}>{apiOnline ? 'LIVE' : 'OFFLINE'}</Text>
                </View>
              </View>

              {!!errorMessage && <InlineNotice message={errorMessage} tone="error" />}
              {ticket.status === 'WAITING' && (
                <Pressable style={styles.cancelButton} onPress={cancelTicket} disabled={isBusy}>
                  {isBusy ? <ActivityIndicator color={colors.red} /> : <Feather name="x-circle" size={17} color={colors.red} />}
                  <Text style={styles.cancelText}>Cancel queue</Text>
                </Pressable>
              )}
              {['COMPLETED', 'CANCELLED', 'NO_SHOW'].includes(ticket.status) && (
                <ActionButton
                  title={isBusy ? 'CLEARING…' : 'DONE'}
                  onPress={() => void handleTicketDone()}
                  disabled={isBusy}
                />
              )}
            </View>
          )}

          <View style={styles.footer}>
            <Text style={styles.footerText}>POLYTECHNIC COLLEGE OF DAVAO DEL SUR</Text>
            <Text style={styles.footerSubtext}>PCDS QUEUE · {apiOnline ? 'LIVE' : 'OFFLINE'}</Text>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
      <View style={styles.bottomNav}>
        <Pressable style={styles.navItem} onPress={() => setScreen('home')}>
          <Feather name="home" size={18} color={screen === 'home' ? colors.green : colors.muted} />
          <Text style={[styles.navText, screen === 'home' && styles.navTextActive]}>Home</Text>
        </Pressable>
        <Pressable
          style={[styles.navItem, !ticket && styles.navItemDisabled]}
          onPress={() => ticket && setScreen('ticket')}
          disabled={!ticket}
          accessibilityRole="tab"
          accessibilityState={{ selected: screen === 'ticket' }}
        >
          <Feather name="hash" size={18} color={screen === 'ticket' ? colors.green : colors.muted} />
          <Text style={[styles.navText, screen === 'ticket' && styles.navTextActive]}>My Queue</Text>
        </Pressable>
        <Pressable
          style={styles.navItem}
          onPress={() => setScreen('settings')}
          accessibilityRole="tab"
          accessibilityState={{ selected: screen === 'settings' }}
        >
          <Feather name="settings" size={18} color={screen === 'settings' ? colors.green : colors.muted} />
          <Text style={[styles.navText, screen === 'settings' && styles.navTextActive]}>Settings</Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );

  return (
    <SafeAreaProvider>
      <ThemeContext.Provider value={appTheme}>
        <View style={styles.appRoot}>
          <StatusBar
            key={`${showSplash ? 'splash' : activeAlert ? 'alert' : screen}-${preferences.darkMode ? 'dark' : 'light'}`}
            barStyle={showSplash || activeAlert || preferences.darkMode ? 'light-content' : 'dark-content'}
            backgroundColor={showSplash ? '#077F8D' : activeAlert ? colors.primarySurface : preferences.darkMode ? colors.background : colors.white}
            translucent={false}
            hidden={false}
          />
          {connectionRestored && (
            <View style={styles.connectionRestoredBanner}>
              <Text style={styles.connectionRestoredText}>CONNECTION RESTORED — queue status is up to date.</Text>
            </View>
          )}
          {appContent}
          {activeAlert && (
            <QueueAlertOverlay alert={activeAlert} onAcknowledge={() => setActiveAlert(null)} />
          )}
          <ConnectionModal
            visible={showConnectionModal}
            connectionType={connectionType}
            ticket={ticket}
            onRetry={() => void checkConnection()}
          />
        </View>
      </ThemeContext.Provider>
    </SafeAreaProvider>
  );
}

function SplashScreen({ status }: { status: string }) {
  const { styles } = useAppTheme();
  return (
    <SafeAreaView style={styles.splashSafeArea}>
      <View style={styles.splashContent}>
        <View style={styles.splashLogoShell}>
          <Image source={require('./assets/pcds-logo.png')} style={styles.splashLogo} resizeMode="contain" />
        </View>
        <Text style={styles.splashTitle}>PCDS QUEUE</Text>
        <Text style={styles.splashSubtitle}>Campus Queue Management System</Text>
        <View style={styles.splashLoadingRow}>
          <ActivityIndicator size="small" color="#FFFFFF" />
          <Text style={styles.splashLoadingText}>{status}</Text>
        </View>
        <View style={styles.splashFooter}>
          <Text style={styles.splashSchool}>POLYTECHNIC COLLEGE OF</Text>
          <Text style={styles.splashSchool}>DAVAO DEL SUR, INC.</Text>
          <Text style={styles.splashLocation}>Digos City</Text>
        </View>
      </View>
    </SafeAreaView>
  );
}

function ConnectionModal({
  visible,
  connectionType,
  ticket,
  onRetry,
}: {
  visible: boolean;
  connectionType: ConnectionType;
  ticket: ActiveTicket | null;
  onRetry: () => void;
}) {
  const { colors, styles } = useAppTheme();
  const noNetwork = connectionType === 'NO_NETWORK';
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onRetry}>
      <View style={styles.connectionOverlay}>
        <View style={styles.connectionCard} accessibilityViewIsModal>
          <Feather name="wifi-off" size={34} color={colors.amber} />
          <Text style={styles.connectionTitle}>CONNECTION LOST</Text>
          <Text style={styles.connectionStatus}>{noNetwork ? 'No Network Connection' : 'Queue Server Offline'}</Text>
          {ticket && <>
            <Text style={styles.connectionLabel}>YOUR QUEUE NUMBER</Text>
            <Text style={styles.connectionNumber}>{ticket.queue_number}</Text>
            <Text style={styles.connectionDepartment}>{typeof ticket.department === 'string' ? ticket.department : ticket.department.department_name}</Text>
          </>}
          <Text style={styles.connectionMessage}>
            {noNetwork
              ? 'Your ticket is saved. Connect to the PCDS network to receive live updates.'
              : 'Your ticket is saved. Live queue updates are temporarily unavailable.'}
          </Text>
          <Pressable style={styles.connectionRetry} onPress={onRetry} accessibilityRole="button" accessibilityLabel="Retry queue server connection">
            <Text style={styles.connectionRetryText}>TRY AGAIN</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}
function QueueAlertOverlay({
  alert,
  onAcknowledge,
}: {
  alert: QueueAlertPresentation;
  onAcknowledge: () => void;
}) {
  const { colors, styles } = useAppTheme();
  const isTest = alert.eventType === 'TEST';
  const isRecall = alert.eventType === 'RECALLED';
  const destination = alert.windowNumber
    ? `Window ${alert.windowNumber}`
    : alert.windowName ?? 'Service window';

  return (
    <SafeAreaView style={[styles.alertOverlay, StyleSheet.absoluteFill]} edges={['top', 'bottom']}>
      <View style={styles.alertContent} accessibilityLiveRegion="assertive">
        <View style={styles.alertIcon}>
          <Feather name={isTest ? 'volume-2' : 'bell'} size={28} color={colors.green} />
        </View>
        <Text style={styles.alertEyebrow}>
          {isTest ? 'TEST ALERT' : isRecall ? 'QUEUE RECALL' : 'YOUR NUMBER IS CALLED'}
        </Text>
        <Text style={styles.alertTitle}>
          {isTest ? 'Check your alert settings' : isRecall ? 'Your number was recalled' : 'Please proceed'}
        </Text>
        <Text style={styles.alertQueueNumber}>{isTest ? 'TEST' : alert.queueNumber}</Text>
        {!isTest && (
          <>
            <Text style={styles.alertPrompt}>PLEASE PROCEED TO</Text>
            <Text style={styles.alertDepartment}>{alert.departmentName.toUpperCase()}</Text>
            <Text style={styles.alertWindow}>{destination.toUpperCase()}</Text>
            <Text style={styles.alertMessage}>
              {isRecall ? 'Your number has been recalled.' : 'Your number is being called.'}
            </Text>
          </>
        )}
        {isTest && (
          <Text style={styles.alertMessage}>This is a PCDS Queue test alert.</Text>
        )}
        <Pressable
          style={styles.alertAcknowledgeButton}
          onPress={onAcknowledge}
          accessibilityRole="button"
          accessibilityLabel="Stop ringing and acknowledge queue alert"
        >
          <Feather name="volume-x" size={19} color={colors.greenDark} />
          <Text style={styles.alertAcknowledgeText}>STOP RINGING · ACKNOWLEDGE</Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

function QrScannerScreen({
  cameraPermissionGranted,
  onRequestCameraPermission,
  onBack,
  onBarcodeFound,
  errorMessage,
  isBusy,
}: {
  cameraPermissionGranted: boolean;
  onRequestCameraPermission: () => void;
  onBack: () => void;
  onBarcodeFound: (data: string) => Promise<boolean>;
  errorMessage: string;
  isBusy: boolean;
}) {
  const { colors, styles } = useAppTheme();
  const scanning = useRef(false);

  const handleBarcodeScanned = (data: string) => {
    if (scanning.current || isBusy) return;
    scanning.current = true;
    void onBarcodeFound(data).then((accepted) => {
      if (!accepted) scanning.current = false;
    });
  };

  return (
    <View style={styles.scannerRoot}>
      {cameraPermissionGranted ? (
        <CameraView
          style={StyleSheet.absoluteFill}
          facing="back"
          barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
          onBarcodeScanned={({ data }) => handleBarcodeScanned(data)}
        />
      ) : (
        <View style={styles.cameraPermissionState}>
          <Feather name="camera" size={34} color={colors.lime} />
          <Text style={styles.scannerTitle}>Camera access needed</Text>
          <Text style={styles.scannerSubtitle}>Allow camera access to scan the department QR.</Text>
          <ActionButton title="Allow camera" onPress={onRequestCameraPermission} />
        </View>
      )}
      <View style={styles.scannerOverlay} pointerEvents="box-none">
        <Pressable
          onPress={onBack}
          style={styles.scannerBack}
          accessibilityRole="button"
          accessibilityLabel="Back to home"
        >
          <Feather name="arrow-left" size={22} color="white" />
        </Pressable>
        <View style={styles.scanFrame} />
        <View style={styles.scannerPrompt}>
          <Text style={styles.scannerTitle}>Scan department QR</Text>
          <Text style={styles.scannerSubtitle}>Hold the code inside the frame.</Text>
          {!!errorMessage && <Text style={styles.scannerError}>{errorMessage}</Text>}
          {isBusy && <ActivityIndicator color={colors.lime} style={{ marginTop: 18 }} />}
        </View>
      </View>
    </View>
  );
}

function QueueInfo({ label, value }: { label: string; value: string }) {
  const { styles } = useAppTheme();
  return (
    <View style={styles.queueInfoItem}>
      <Text style={styles.queueInfoValue}>{value}</Text>
      <Text style={styles.queueInfoLabel}>{label}</Text>
    </View>
  );
}

function SettingsScreen({
  preferences,
  onChange,
  onTestAlert,
  apiOnline,
  serverAddress,
  errorMessage,
  notificationsEnabled,
  notificationsCanAskAgain,
  runningInExpoGo,
  onEnableNotifications,
  onOpenNotificationSettings,
}: {
  preferences: MobilePreferences;
  onChange: (updates: Partial<MobilePreferences>) => void;
  onTestAlert: () => void;
  apiOnline: boolean;
  serverAddress: string;
  errorMessage: string;
  notificationsEnabled: boolean;
  notificationsCanAskAgain: boolean;
  runningInExpoGo: boolean;
  onEnableNotifications: () => void;
  onOpenNotificationSettings: () => void;
}) {
  const { colors, styles } = useAppTheme();
  return (
    <View style={styles.settingsContent}>
      <View style={styles.settingsHeading}>
        <Text style={styles.eyebrow}>PERSONAL PREFERENCES</Text>
        <Text style={styles.sectionTitle}>Settings</Text>
        <Text style={styles.settingsDescription}>Choose how this phone keeps you updated.</Text>
      </View>

      <SettingsSection title="Appearance">
        <SettingsToggle
          icon={preferences.darkMode ? 'moon' : 'sun'}
          title="Dark Mode"
          description="Use a darker color theme throughout the app"
          value={preferences.darkMode}
          onValueChange={(darkMode) => onChange({ darkMode })}
        />
      </SettingsSection>

      <SettingsSection title="Notifications">
        <SettingsToggle
          icon="bell"
          title="Queue Alerts"
          description="Show alerts when your number is called or recalled"
          value={preferences.queueAlerts}
          onValueChange={(queueAlerts) => onChange({ queueAlerts })}
        />
        <SettingsToggle
          icon="repeat"
          title="Recall Alert"
          description="Repeat sound, vibration, and voice when recalled"
          value={preferences.recallAlerts}
          disabled={!preferences.queueAlerts}
          onValueChange={(recallAlerts) => onChange({ recallAlerts })}
        />
        <SettingsToggle
          icon="smartphone"
          title="Vibrate When Called"
          description="Vibrate until you acknowledge the alert"
          value={preferences.vibration}
          disabled={!preferences.queueAlerts}
          onValueChange={(vibration) => onChange({ vibration })}
        />
        <SettingsToggle
          icon="volume-2"
          title="Ring When Called"
          description="Repeat the alert sound until acknowledged"
          value={preferences.alertSound}
          disabled={!preferences.queueAlerts}
          onValueChange={(alertSound) => onChange({ alertSound })}
        />
        <View style={styles.settingSliderBlock}>
          <View style={styles.settingSliderHeading}>
            <Text style={styles.settingsLabel}>RING VOLUME</Text>
            <Text style={styles.sliderValue}>{Math.round(preferences.ringVolume * 100)}%</Text>
          </View>
          <Slider
            accessibilityLabel="Queue alert ring volume"
            minimumValue={0}
            maximumValue={1}
            step={0.05}
            value={preferences.ringVolume}
            minimumTrackTintColor={colors.green}
            maximumTrackTintColor={colors.line}
            thumbTintColor={colors.green}
            disabled={!preferences.queueAlerts || !preferences.alertSound}
            onValueChange={(ringVolume) => onChange({ ringVolume })}
          />
          <Text style={styles.settingsToggleDescription}>
            Adjusts this app only, not the system volume.
          </Text>
        </View>
        <SettingsToggle
          icon="mic"
          title="Voice Announcement"
          description="Speak your queue number and service window"
          value={preferences.voiceAnnouncement}
          disabled={!preferences.queueAlerts}
          onValueChange={(voiceAnnouncement) => onChange({ voiceAnnouncement })}
        />
        <Pressable
          style={[styles.testAlertButton, !preferences.queueAlerts && styles.disabledButton]}
          onPress={onTestAlert}
          disabled={!preferences.queueAlerts}
          accessibilityRole="button"
          accessibilityLabel="Test queue alert"
        >
          <Feather name="play" size={16} color={colors.green} />
          <Text style={styles.testAlertButtonText}>TEST ALERT</Text>
        </Pressable>
      </SettingsSection>

      <SettingsSection title="Queue Alerts">
        <View style={styles.backgroundAlertRow}>
          <View style={styles.settingsIcon}>
            <Feather name={notificationsEnabled && !runningInExpoGo ? 'check-circle' : 'alert-triangle'} size={17} color={notificationsEnabled && !runningInExpoGo ? colors.green : colors.amber} />
          </View>
          <View style={styles.settingsToggleCopy}>
            <Text style={styles.settingsToggleTitle}>Background Alerts</Text>
            <Text style={styles.settingsToggleDescription}>
              {runningInExpoGo
                ? 'Android push notifications require a development build'
                : notificationsEnabled
                ? 'Allowed by Android'
                : 'Permission required so you do not miss your queue call'}
            </Text>
          </View>
          {!runningInExpoGo && !notificationsEnabled && (
            <Pressable
              style={styles.backgroundAlertAction}
              onPress={notificationsCanAskAgain ? onEnableNotifications : onOpenNotificationSettings}
              accessibilityRole="button"
              accessibilityLabel={notificationsCanAskAgain ? 'Enable background alerts' : 'Open Android notification settings'}
            >
              <Text style={styles.backgroundAlertActionText}>
                {notificationsCanAskAgain ? 'ENABLE' : 'OPEN SETTINGS'}
              </Text>
            </Pressable>
          )}
        </View>
      </SettingsSection>

      <SettingsSection title="Voice">
        <Text style={styles.settingsLabel}>VOICE LANGUAGE</Text>
        <View style={styles.optionRow}>
          <OptionButton
            label="English"
            selected={preferences.voiceLanguage === 'en-US'}
            onPress={() => onChange({ voiceLanguage: 'en-US' })}
          />
          <OptionButton
            label="Filipino"
            selected={preferences.voiceLanguage === 'fil-PH'}
            onPress={() => onChange({ voiceLanguage: 'fil-PH' })}
          />
        </View>
        <View style={styles.settingSliderHeading}>
          <Text style={styles.settingsLabel}>VOICE VOLUME</Text>
          <Text style={styles.sliderValue}>{Math.round(preferences.voiceVolume * 100)}%</Text>
        </View>
        <Slider
          accessibilityLabel="Voice announcement volume"
          minimumValue={0}
          maximumValue={1}
          step={0.05}
          value={preferences.voiceVolume}
          minimumTrackTintColor={colors.green}
          maximumTrackTintColor={colors.line}
          thumbTintColor={colors.green}
          disabled={!preferences.queueAlerts || !preferences.voiceAnnouncement}
          onValueChange={(voiceVolume) => onChange({ voiceVolume })}
        />
      </SettingsSection>

      <SettingsSection title="Queue">
        <SettingsToggle
          icon="refresh-cw"
          title="Auto Refresh"
          description="Automatically update your queue status"
          value={preferences.autoRefresh}
          onValueChange={(autoRefresh) => onChange({ autoRefresh })}
        />
        {preferences.autoRefresh && (
          <View style={styles.settingOptionBlock}>
            <Text style={styles.settingsLabel}>REFRESH INTERVAL</Text>
            <View style={styles.optionRow}>
              {[3, 5, 10].map((seconds) => (
                <OptionButton
                  key={seconds}
                  label={`${seconds} sec`}
                  selected={preferences.refreshIntervalSeconds === seconds}
                  onPress={() => onChange({ refreshIntervalSeconds: seconds })}
                />
              ))}
            </View>
          </View>
        )}
        <SettingsToggle
          icon="sun"
          title="Keep Screen Awake"
          description="Keep the display on while waiting in the queue"
          value={preferences.keepScreenAwake}
          onValueChange={(keepScreenAwake) => onChange({ keepScreenAwake })}
        />
      </SettingsSection>

      <SettingsSection title="Connection">
        <View style={styles.connectionSetting}>
          <View style={[styles.connectionDot, { backgroundColor: apiOnline ? colors.green : colors.red }]} />
          <Text style={styles.connectionSettingText}>
            {apiOnline ? 'Queue Server Connected' : 'Queue Server Offline'}
          </Text>
        </View>
        <Text style={styles.settingsLabel}>SERVER</Text>
        <Text style={styles.connectionAddress}>{serverAddress || 'PCDS Local Network'}</Text>
      </SettingsSection>

      <SettingsSection title="About">
        <View style={styles.aboutRow}>
          <View style={styles.brandMark}><Image source={require('./assets/pcds-logo.png')} style={styles.brandLogo} resizeMode="contain" /></View>
          <View style={styles.flex}>
            <Text style={styles.aboutName}>PCDS Queue</Text>
            <Text style={styles.aboutVersion}>Version 1.0.0</Text>
          </View>
        </View>
      </SettingsSection>
      {!!errorMessage && <InlineNotice message={errorMessage} tone="error" />}
    </View>
  );
}

function SettingsSection({ title, children }: { title: string; children: React.ReactNode }) {
  const { styles } = useAppTheme();
  return (
    <View style={styles.settingsSection}>
      <Text style={styles.settingsSectionTitle}>{title}</Text>
      <View style={styles.settingsCard}>{children}</View>
    </View>
  );
}

function SettingsToggle({
  icon,
  title,
  description,
  value,
  disabled = false,
  onValueChange,
}: {
  icon: React.ComponentProps<typeof Feather>['name'];
  title: string;
  description: string;
  value: boolean;
  disabled?: boolean;
  onValueChange: (value: boolean) => void;
}) {
  const { colors, styles } = useAppTheme();
  return (
    <View style={[styles.settingsToggleRow, disabled && styles.settingsToggleDisabled]}>
      <View style={styles.settingsIcon}>
        <Feather name={icon} size={17} color={disabled ? colors.faint : colors.green} />
      </View>
      <View style={styles.settingsToggleCopy}>
        <Text style={styles.settingsToggleTitle}>{title}</Text>
        <Text style={styles.settingsToggleDescription}>{description}</Text>
      </View>
      <Switch
        value={value}
        disabled={disabled}
        onValueChange={onValueChange}
        trackColor={{ false: colors.line, true: colors.green }}
        thumbColor={value ? colors.green : colors.surface}
        ios_backgroundColor={colors.line}
        accessibilityLabel={title}
      />
    </View>
  );
}

function OptionButton({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  const { styles } = useAppTheme();
  return (
    <Pressable
      style={[styles.optionButton, selected && styles.optionButtonSelected]}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
    >
      <Text style={[styles.optionButtonText, selected && styles.optionButtonTextSelected]}>{label}</Text>
    </Pressable>
  );
}

function ActiveTicketCard({
  ticket,
  announcement,
  onPress,
}: {
  ticket: ActiveTicket;
  announcement: QueueAnnouncementEvent | null;
  onPress: () => void;
}) {
  const { colors, styles } = useAppTheme();
  const ticketMessage = announcement?.queue_id === ticket.queue_id &&
    announcement.event_type === 'RECALLED'
    ? `Recalled · Proceed to ${announcement.window_name ?? 'the service window'}`
    : ticket.status === 'WAITING'
    ? `${ticket.people_ahead} people ahead`
    : ticket.status === 'CALLED'
      ? `Proceed to ${ticket.window_name ?? 'the service window'}`
      : ticket.status === 'SERVING'
        ? `Being served${ticket.window_name ? ` at ${ticket.window_name}` : ''}`
        : ticket.window_name ?? ticket.status;

  return (
    <Pressable style={styles.activeTicketCard} onPress={onPress}>
      <View style={styles.activeTicketTop}>
        <Text style={styles.activeTicketEyebrow}>ACTIVE TICKET</Text>
        <StatusPill status={ticket.status} />
      </View>
      <View style={styles.activeTicketBottom}>
        <Text style={styles.activeTicketNumber}>{ticket.queue_number}</Text>
        <View style={styles.flex}>
          <Text style={styles.activeTicketDepartment}>{ticket.department.department_name}</Text>
          <Text style={styles.activeTicketMeta}>{ticketMessage}</Text>
        </View>
        <Feather name="arrow-up-right" size={20} color={colors.green} />
      </View>
    </Pressable>
  );
}

function StatusPill({ status }: { status: string }) {
  const { colors, styles } = useAppTheme();
  const waiting = status === 'WAITING';
  const active = ['CALLED', 'SERVING'].includes(status);
  const backgroundColor = waiting ? colors.amberBg : active ? colors.mint : colors.inactiveStatus;
  const textColor = waiting ? colors.amber : active ? colors.green : colors.muted;
  const label = status === 'SERVING' ? 'NOW SERVING' : status.replace('_', ' ');
  return (
    <View style={[styles.statusPill, { backgroundColor }]}>
      <View style={[styles.statusDot, { backgroundColor: textColor }]} />
      <Text style={[styles.statusText, { color: textColor }]}>{label}</Text>
    </View>
  );
}

function InlineNotice({ message, tone }: { message: string; tone: 'error' | 'warning' | 'success' }) {
  const { colors, styles } = useAppTheme();
  const palette = tone === 'error'
    ? { background: colors.redBg, text: colors.red, icon: 'alert-circle' as const }
    : tone === 'warning'
      ? { background: colors.amberBg, text: colors.amber, icon: 'alert-triangle' as const }
      : { background: colors.mint, text: colors.green, icon: 'check-circle' as const };
  return (
    <View style={[styles.notice, { backgroundColor: palette.background }]}>
      <Feather name={palette.icon} size={17} color={palette.text} />
      <Text style={[styles.noticeText, { color: palette.text }]}>{message}</Text>
    </View>
  );
}

function ActionButton({
  title,
  onPress,
  disabled = false,
  icon,
}: {
  title: string;
  onPress: () => void;
  disabled?: boolean;
  icon?: React.ComponentProps<typeof Feather>['name'];
}) {
  const { colors, styles } = useAppTheme();
  return (
    <Pressable style={[styles.actionButton, disabled && styles.disabledButton]} onPress={onPress} disabled={disabled}>
      <Text style={styles.actionButtonText}>{title}</Text>
      {icon && <Feather name={icon} size={18} color={colors.iconOnPrimary} />}
    </Pressable>
  );
}

function formatGraceTime(seconds: number): string {
  const safeSeconds = Math.max(0, seconds || 0);
  return `${String(Math.floor(safeSeconds / 60)).padStart(2, '0')}:${String(safeSeconds % 60).padStart(2, '0')}`;
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.';
}

function isRestorableTicket(
  value: unknown,
): value is Omit<ActiveTicket, 'deviceIdentifier'> & Partial<Pick<ActiveTicket, 'deviceIdentifier'>> {
  if (!value || typeof value !== 'object') return false;
  const saved = value as Record<string, unknown>;
  const department = saved.department;
  if (!department || typeof department !== 'object') return false;
  const savedDepartment = department as Record<string, unknown>;
  return Number.isSafeInteger(saved.queue_id) &&
    (saved.queue_id as number) > 0 &&
    typeof saved.queue_number === 'string' &&
    typeof saved.sequence_number === 'number' &&
    typeof saved.source === 'string' &&
    typeof saved.status === 'string' &&
    typeof saved.people_ahead === 'number' &&
    typeof savedDepartment.department_id === 'number' &&
    typeof savedDepartment.department_name === 'string' &&
    typeof savedDepartment.queue_prefix === 'string';
}

function withoutStatusDepartment(status: QueueStatus): Omit<QueueStatus, 'department'> {
  const { department: _departmentName, ...statusFields } = status;
  return statusFields;
}

function parseMobilePreferences(serialized: string | null): MobilePreferences {
  if (!serialized) return DEFAULT_PREFERENCES;

  const saved = JSON.parse(serialized) as Partial<MobilePreferences>;
  const refreshIntervals = [3, 5, 10];
  return {
    queueAlerts: typeof saved.queueAlerts === 'boolean' ? saved.queueAlerts : DEFAULT_PREFERENCES.queueAlerts,
    recallAlerts: typeof saved.recallAlerts === 'boolean'
      ? saved.recallAlerts
      : DEFAULT_PREFERENCES.recallAlerts,
    vibration: typeof saved.vibration === 'boolean' ? saved.vibration : DEFAULT_PREFERENCES.vibration,
    alertSound: typeof saved.alertSound === 'boolean' ? saved.alertSound : DEFAULT_PREFERENCES.alertSound,
    ringVolume: typeof saved.ringVolume === 'number' && Number.isFinite(saved.ringVolume)
      ? Math.max(0, Math.min(1, saved.ringVolume))
      : DEFAULT_PREFERENCES.ringVolume,
    voiceAnnouncement: typeof saved.voiceAnnouncement === 'boolean'
      ? saved.voiceAnnouncement
      : DEFAULT_PREFERENCES.voiceAnnouncement,
    voiceLanguage: saved.voiceLanguage === 'fil-PH' ? 'fil-PH' : 'en-US',
    voiceVolume: typeof saved.voiceVolume === 'number' && Number.isFinite(saved.voiceVolume)
      ? Math.max(0, Math.min(1, saved.voiceVolume))
      : DEFAULT_PREFERENCES.voiceVolume,
    autoRefresh: typeof saved.autoRefresh === 'boolean' ? saved.autoRefresh : DEFAULT_PREFERENCES.autoRefresh,
    refreshIntervalSeconds: refreshIntervals.includes(saved.refreshIntervalSeconds ?? -1)
      ? saved.refreshIntervalSeconds as number
      : DEFAULT_PREFERENCES.refreshIntervalSeconds,
    keepScreenAwake: typeof saved.keepScreenAwake === 'boolean'
      ? saved.keepScreenAwake
      : DEFAULT_PREFERENCES.keepScreenAwake,
    darkMode: typeof saved.darkMode === 'boolean' ? saved.darkMode : DEFAULT_PREFERENCES.darkMode,
  };
}

function createQueueAnnouncementMessage(alert: QueueAlertPresentation): string {
  const windowLabel = alert.windowNumber
    ? `${alert.departmentName}, Window ${alert.windowNumber}`
    : alert.windowName ?? `${alert.departmentName} service window`;
  return alert.eventType === 'RECALLED'
    ? `Your queue number ${alert.queueNumber} is being recalled. Please proceed to ${windowLabel}.`
    : `Your queue number ${alert.queueNumber} is being called. Please proceed to ${windowLabel}.`;
}

function createStyles(colors: ThemeColors, width: number) {
  return StyleSheet.create({
  appRoot: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1 },
  safeArea: { flex: 1, backgroundColor: colors.background },
  scrollContent: {
    flexGrow: 1,
    width: '100%',
    maxWidth: 640,
    alignSelf: 'center',
    paddingHorizontal: width < 360 ? 16 : width >= 768 ? 32 : 22,
    paddingBottom: 22,
  },
  header: { minHeight: 74, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  brandRow: { flexDirection: 'row', alignItems: 'center', gap: 11 },
  brandMark: { width: 42, height: 42, borderRadius: 10, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  brandLetter: { color: colors.green, fontSize: 20, fontWeight: '900' },
  brandLogo: { width: 34, height: 34 },
  splashSafeArea: { flex: 1, backgroundColor: '#077F8D' },
  splashContent: { flex: 1, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 30 },
  splashLogoShell: { width: 166, height: 166, borderRadius: 83, backgroundColor: '#FFFFFF', alignItems: 'center', justifyContent: 'center', shadowColor: '#000000', shadowOffset: { width: 0, height: 6 }, shadowOpacity: 0.2, shadowRadius: 8, elevation: 8 },
  splashLogo: { width: 148, height: 148 },
  splashTitle: { color: '#FFFFFF', fontSize: 31, fontWeight: '900', letterSpacing: 2, marginTop: 25 },
  splashSubtitle: { color: '#D8F1F3', fontSize: 15, textAlign: 'center', marginTop: 7 },
  splashLoadingRow: { flexDirection: 'row', alignItems: 'center', marginTop: 40 },
  splashLoadingText: { color: '#FFFFFF', marginLeft: 12, fontSize: 13 },
  splashFooter: { position: 'absolute', bottom: 45, alignItems: 'center' },
  splashSchool: { color: '#FFFFFF', fontSize: 11, fontWeight: '700', letterSpacing: 1 },
  splashLocation: { color: '#BDE5E8', fontSize: 11, marginTop: 5 },
  connectionRestoredBanner: { position: 'absolute', top: 52, left: 16, right: 16, backgroundColor: colors.mint, borderColor: colors.green, borderWidth: 1, borderRadius: 10, padding: 12, zIndex: 20 },
  connectionRestoredText: { color: colors.greenDark, fontSize: 12, fontWeight: '800', textAlign: 'center' },
  connectionOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.55)', justifyContent: 'center', padding: width < 360 ? 16 : 24 },
  connectionCard: { width: '100%', maxWidth: 440, alignSelf: 'center', backgroundColor: colors.surface, borderRadius: 18, padding: width < 360 ? 20 : 28, alignItems: 'center' },
  connectionTitle: { color: colors.amber, fontSize: 13, fontWeight: '900', letterSpacing: 1.2, marginTop: 10 },
  connectionStatus: { color: colors.ink, fontSize: 21, fontWeight: '900', marginTop: 8, textAlign: 'center' },
  connectionLabel: { color: colors.muted, fontSize: 11, fontWeight: '700', marginTop: 22 },
  connectionNumber: { color: colors.green, fontSize: 46, fontWeight: '900', marginTop: 2 },
  connectionDepartment: { color: colors.muted, fontSize: 14, fontWeight: '700' },
  connectionMessage: { color: colors.muted, fontSize: 13, lineHeight: 20, textAlign: 'center', marginTop: 20 },
  connectionRetry: { width: '100%', minHeight: 48, backgroundColor: colors.primarySurface, borderRadius: 10, justifyContent: 'center', alignItems: 'center', marginTop: 24 },
  connectionRetryText: { color: colors.white, fontSize: 13, fontWeight: '900', letterSpacing: 0.6 },
  brandName: { color: colors.ink, fontSize: 14, fontWeight: '800', letterSpacing: 1.1 },
  brandCaption: { color: colors.muted, fontSize: 9, fontWeight: '700', marginTop: 3, letterSpacing: 0.8 },
  connectionRow: { flexDirection: 'row', alignItems: 'center', minHeight: 31, gap: 7, borderBottomWidth: 1, borderBottomColor: colors.line, paddingBottom: 10 },
  connectionDot: { width: 7, height: 7, borderRadius: 4 },
  connectionText: { color: colors.muted, fontSize: 9, fontWeight: '800', letterSpacing: 0.7, flex: 1 },
  hero: { paddingTop: 36, paddingBottom: 26 },
  eyebrow: { color: colors.green, fontSize: 10, fontWeight: '800', letterSpacing: 1.2 },
  heroTitle: { color: colors.ink, fontSize: 38, fontWeight: '800', marginTop: 9 },
  heroCopy: { color: colors.muted, fontSize: 15, lineHeight: 22, marginTop: 8, maxWidth: 310 },
  scanButton: { minHeight: 84, backgroundColor: colors.primarySurface, borderRadius: 9, paddingHorizontal: 16, flexDirection: 'row', alignItems: 'center', gap: 13, marginTop: 14 },
  disabledButton: { opacity: 0.48 },
  scanIcon: { width: 46, height: 46, borderRadius: 9, backgroundColor: 'rgba(255,255,255,0.16)', alignItems: 'center', justifyContent: 'center' },
  scanButtonText: { flex: 1 },
  scanButtonTitle: { color: colors.scanButtonText, fontSize: 12, fontWeight: '900', letterSpacing: 0.8 },
  scanButtonCaption: { color: colors.scanCaption, fontSize: 11, marginTop: 5 },
  serviceNote: { marginTop: 37 },
  noteRule: { height: 1, backgroundColor: colors.line, marginBottom: 17 },
  noteLabel: { color: colors.muted, fontSize: 9, fontWeight: '800', letterSpacing: 1 },
  noteText: { color: colors.ink, fontSize: 14, fontWeight: '600', marginTop: 8 },
  serviceError: { color: colors.red, fontSize: 12, lineHeight: 17, marginTop: 6 },
  footer: { marginTop: 'auto', paddingTop: 38, alignItems: 'center' },
  footerText: { color: colors.faint, fontSize: 8, fontWeight: '800', letterSpacing: 0.7 },
  footerSubtext: { color: colors.faint, fontSize: 8, marginTop: 5 },
  activeTicketCard: { backgroundColor: colors.surface, borderRadius: 9, padding: 17, borderWidth: 1, borderColor: colors.line, marginBottom: 5 },
  activeTicketTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  activeTicketEyebrow: { color: colors.muted, fontWeight: '800', fontSize: 9, letterSpacing: 0.9 },
  activeTicketBottom: { flexDirection: 'row', alignItems: 'center', gap: 13, marginTop: 17 },
  activeTicketNumber: { color: colors.green, fontSize: 29, fontWeight: '900' },
  activeTicketDepartment: { color: colors.ink, fontSize: 13, fontWeight: '800' },
  activeTicketMeta: { color: colors.muted, fontSize: 11, marginTop: 4 },
  announcementCard: { flexDirection: 'row', alignItems: 'flex-start', gap: 12, padding: 16, backgroundColor: colors.amberBg, borderRadius: 10, borderWidth: 1, borderColor: colors.amberBorder, marginTop: 16 },
  announcementIcon: { width: 42, height: 42, borderRadius: 10, backgroundColor: colors.surface, alignItems: 'center', justifyContent: 'center' },
  announcementEyebrow: { color: colors.amber, fontSize: 10, fontWeight: '900', letterSpacing: 0.9 },
  announcementNumber: { color: colors.ink, fontSize: 28, fontWeight: '900', marginTop: 4 },
  announcementText: { color: colors.muted, fontSize: 12, marginTop: 5 },
  announcementDestination: { color: colors.greenDark, fontSize: 12, fontWeight: '900', marginTop: 3 },
  announcementFooter: { color: colors.ink, fontSize: 11, fontWeight: '600', marginTop: 8 },
  screenSection: { paddingTop: 22, flex: 1 },
  settingsContent: { paddingTop: 24, paddingBottom: 20 },
  settingsHeading: { marginBottom: 8 },
  settingsDescription: { color: colors.muted, fontSize: 13, lineHeight: 19, marginTop: 8 },
  settingsSection: { marginTop: 24 },
  settingsSectionTitle: { color: colors.ink, fontSize: 16, fontWeight: '800', marginBottom: 10 },
  settingsCard: { backgroundColor: colors.surface, borderRadius: 10, borderWidth: 1, borderColor: colors.line, paddingHorizontal: 14, paddingVertical: 6 },
  settingsToggleRow: { flexDirection: 'row', alignItems: 'center', gap: 11, minHeight: 76, borderBottomWidth: 1, borderBottomColor: colors.line },
  settingsToggleDisabled: { opacity: 0.5 },
  settingsIcon: { width: 38, height: 38, borderRadius: 9, backgroundColor: colors.mint, alignItems: 'center', justifyContent: 'center' },
  settingsToggleCopy: { flex: 1, paddingVertical: 10 },
  settingsToggleTitle: { color: colors.ink, fontSize: 13, fontWeight: '700' },
  settingsToggleDescription: { color: colors.muted, fontSize: 11, lineHeight: 15, marginTop: 3 },
  settingsLabel: { color: colors.muted, fontSize: 9, fontWeight: '800', letterSpacing: 0.7 },
  settingSliderBlock: { paddingVertical: 15, borderBottomWidth: 1, borderBottomColor: colors.line },
  testAlertButton: { minHeight: 48, borderRadius: 8, borderWidth: 1, borderColor: colors.green, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, marginVertical: 10 },
  testAlertButtonText: { color: colors.green, fontSize: 11, fontWeight: '900', letterSpacing: 0.7 },
  settingOptionBlock: { paddingVertical: 15, borderBottomWidth: 1, borderBottomColor: colors.line },
  optionRow: { flexDirection: 'row', gap: 9, marginTop: 10 },
  optionButton: { minHeight: 40, minWidth: 72, paddingHorizontal: 13, borderRadius: 8, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surface },
  optionButtonSelected: { borderColor: colors.green, backgroundColor: colors.mint },
  optionButtonText: { color: colors.muted, fontSize: 12, fontWeight: '700' },
  optionButtonTextSelected: { color: colors.green, fontWeight: '800' },
  settingSliderHeading: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 19 },
  sliderValue: { color: colors.green, fontSize: 12, fontWeight: '800' },
  connectionSetting: { flexDirection: 'row', alignItems: 'center', gap: 8, minHeight: 42 },
  connectionSettingText: { color: colors.ink, fontSize: 13, fontWeight: '700' },
  connectionAddress: { color: colors.ink, fontSize: 13, fontWeight: '700', marginTop: 6, marginBottom: 8 },
  aboutRow: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 8 },
  aboutName: { color: colors.ink, fontSize: 13, fontWeight: '800' },
  aboutVersion: { color: colors.muted, fontSize: 11, marginTop: 4 },
  backRow: { flexDirection: 'row', alignItems: 'center', gap: 7, minHeight: 38, marginBottom: 23 },
  backText: { color: colors.green, fontSize: 13, fontWeight: '700' },
  sectionTitle: { color: colors.ink, fontSize: 29, fontWeight: '800', marginTop: 7 },
  departmentCard: { flexDirection: 'row', alignItems: 'center', gap: 14, padding: 17, backgroundColor: colors.surface, borderRadius: 9, borderWidth: 1, borderColor: colors.line, marginTop: 26 },
  departmentPrefix: { width: 52, height: 52, borderRadius: 9, backgroundColor: colors.mint, alignItems: 'center', justifyContent: 'center' },
  prefixText: { color: colors.green, fontSize: 24, fontWeight: '900' },
  departmentName: { color: colors.ink, fontSize: 17, fontWeight: '800' },
  departmentMeta: { color: colors.muted, fontSize: 11, marginTop: 4 },
  helperText: { color: colors.muted, fontSize: 13, lineHeight: 19, marginTop: 16, marginBottom: 18 },
  queueInfoGrid: { flexDirection: 'row', backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.line, borderRadius: 9, paddingVertical: 14, marginTop: 2 },
  queueInfoItem: { flex: 1, alignItems: 'center', paddingHorizontal: 5 },
  queueInfoValue: { color: colors.ink, fontSize: 17, fontWeight: '800' },
  queueInfoLabel: { color: colors.muted, fontSize: 8, fontWeight: '800', letterSpacing: 0.45, textAlign: 'center', marginTop: 5 },
  actionButton: { minHeight: 52, borderRadius: 9, backgroundColor: colors.primarySurface, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10, paddingHorizontal: 18, marginTop: 16 },
  actionButtonText: { color: colors.white, fontSize: 12, fontWeight: '900', letterSpacing: 0.6 },
  ticketHeading: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  statusPill: { minHeight: 27, borderRadius: 99, paddingHorizontal: 10, flexDirection: 'row', alignItems: 'center', gap: 6 },
  statusDot: { width: 6, height: 6, borderRadius: 3 },
  statusText: { fontSize: 9, fontWeight: '900', letterSpacing: 0.3 },
  callGraceCard: { backgroundColor: colors.primarySurface, borderRadius: 10, padding: 20, marginTop: 16, alignItems: 'center' },
  callGraceLabel: { color: colors.onPrimaryMuted, fontSize: 10, fontWeight: '800', letterSpacing: 1 },
  callGraceTimer: { color: colors.onPrimary, fontSize: 42, fontWeight: '900', marginTop: 4 },
  callGraceButton: { width: '100%', backgroundColor: colors.surface, borderRadius: 9, paddingVertical: 15, alignItems: 'center', marginTop: 16 },
  callGraceButtonText: { color: colors.greenDark, fontSize: 12, fontWeight: '900' },
  callGraceAcknowledged: { color: colors.lime, fontSize: 12, fontWeight: '900', marginTop: 16, textAlign: 'center' },
  notificationWarning: { flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: colors.amberBg, borderColor: colors.amberBorder, borderWidth: 1, borderRadius: 10, padding: 13, marginTop: 14 },
  notificationWarningTitle: { color: colors.amber, fontSize: 13, fontWeight: '800' },
  notificationWarningText: { color: colors.muted, fontSize: 11, lineHeight: 16, marginTop: 3 },
  notificationEnableButton: { minHeight: 48, paddingHorizontal: 12, borderRadius: 8, backgroundColor: colors.primarySurface, alignItems: 'center', justifyContent: 'center' },
  notificationEnableButtonText: { color: colors.white, fontSize: 11, fontWeight: '900' },
  backgroundAlertRow: { flexDirection: 'row', alignItems: 'center', gap: 11, paddingVertical: 3 },
  backgroundAlertAction: { minHeight: 48, borderRadius: 8, backgroundColor: colors.primarySurface, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 10 },
  backgroundAlertActionText: { color: colors.white, fontSize: 9, fontWeight: '900', letterSpacing: 0.2 },
  ticketCard: { backgroundColor: colors.primarySurface, borderRadius: 9, padding: 22, marginTop: 22 },
  ticketLabel: { color: colors.onPrimaryMuted, fontSize: 9, fontWeight: '800', letterSpacing: 1.1, textAlign: 'center' },
  ticketNumber: { color: colors.onPrimary, fontSize: 58, fontWeight: '900', textAlign: 'center', marginTop: 9 },
  ticketDivider: { height: 1, backgroundColor: colors.primaryDivider, marginVertical: 20 },
  ticketStats: { flexDirection: 'row', alignItems: 'center' },
  ticketStat: { flex: 1, alignItems: 'center' },
  statValue: { color: colors.onPrimary, fontSize: 23, fontWeight: '800' },
  statLabel: { color: colors.onPrimaryMuted, fontSize: 8, fontWeight: '800', letterSpacing: 0.7, marginTop: 5 },
  currentServingLabel: { color: colors.muted, fontSize: 8, fontWeight: '800', letterSpacing: 0.7, marginTop: 5 },
  statDivider: { width: 1, height: 37, backgroundColor: 'rgba(255,255,255,0.2)' },
  currentServingCard: { flexDirection: 'row', alignItems: 'center', gap: 13, padding: 16, backgroundColor: colors.surface, borderRadius: 9, borderWidth: 1, borderColor: colors.line, marginTop: 14 },
  servingIcon: { width: 39, height: 39, borderRadius: 9, backgroundColor: colors.mint, alignItems: 'center', justifyContent: 'center' },
  currentServingNumber: { color: colors.ink, fontSize: 18, fontWeight: '800', marginTop: 2 },
  liveStatus: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  liveStatusDot: { width: 7, height: 7, borderRadius: 4 },
  liveStatusText: { color: colors.muted, fontSize: 9, fontWeight: '800', letterSpacing: 0.5 },
  windowText: { color: colors.muted, fontSize: 11, marginTop: 3 },
  cancelButton: { minHeight: 48, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, marginTop: 8 },
  cancelText: { color: colors.red, fontSize: 12, fontWeight: '800' },
  notice: { borderRadius: 13, padding: 13, flexDirection: 'row', alignItems: 'center', gap: 9, marginTop: 14 },
  noticeText: { flex: 1, fontSize: 12, lineHeight: 18, fontWeight: '600' },
  scannerRoot: { flex: 1, backgroundColor: colors.primarySurface },
  // Keep the live camera preview completely unobstructed. Only controls get a
  // neutral scrim so labels stay readable without tinting the camera green.
  scannerOverlay: { ...StyleSheet.absoluteFill, alignItems: 'center', justifyContent: 'center', padding: 26 },
  scannerBack: { position: 'absolute', top: 60, left: 22, width: 48, height: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 16, backgroundColor: colors.scanBack },
  scanFrame: { width: Math.min(250, width - 64), height: Math.min(250, width - 64), borderRadius: 16, borderWidth: 3, borderColor: colors.white, marginBottom: 29, shadowColor: '#000000', shadowOpacity: 0.45, shadowRadius: 8, elevation: 7 },
  scannerPrompt: { alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.58)', borderRadius: 16, paddingHorizontal: 20, paddingVertical: 16, maxWidth: '100%' },
  scannerTitle: { color: colors.white, fontSize: 20, fontWeight: '800', textAlign: 'center' },
  scannerSubtitle: { color: colors.scanCaption, fontSize: 13, marginTop: 8, textAlign: 'center' },
  scannerError: { color: '#FFD1C9', textAlign: 'center', fontSize: 12, lineHeight: 18, marginTop: 16 },
  cameraPermissionState: { ...StyleSheet.absoluteFill, alignItems: 'center', justifyContent: 'center', padding: 30, gap: 10 },
  bottomNav: { width: '100%', maxWidth: 640, alignSelf: 'center', flexDirection: 'row', backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.line, paddingVertical: 6 },
  navItem: { flex: 1, alignItems: 'center', gap: 4, minHeight: 48, justifyContent: 'center' },
  navItemDisabled: { opacity: 0.45 },
  navText: { color: colors.muted, fontSize: 10, fontWeight: '700' },
  navTextActive: { color: colors.green },
  alertOverlay: { zIndex: 100, backgroundColor: colors.primarySurface, justifyContent: 'center', paddingHorizontal: 24 },
  alertContent: { alignItems: 'center' },
  alertIcon: { width: 64, height: 64, borderRadius: 20, backgroundColor: colors.surface, alignItems: 'center', justifyContent: 'center', marginBottom: 24 },
  alertEyebrow: { color: colors.onPrimaryMuted, fontSize: 11, fontWeight: '900', letterSpacing: 1.3, textAlign: 'center' },
  alertTitle: { color: colors.onPrimary, fontSize: 25, fontWeight: '900', textAlign: 'center', marginTop: 10 },
  alertQueueNumber: { color: colors.onPrimary, fontSize: 58, fontWeight: '900', textAlign: 'center', marginTop: 22 },
  alertPrompt: { color: colors.onPrimaryMuted, fontSize: 10, fontWeight: '800', letterSpacing: 1, marginTop: 14 },
  alertDepartment: { color: colors.onPrimary, fontSize: 19, fontWeight: '900', textAlign: 'center', marginTop: 9 },
  alertWindow: { color: colors.onPrimary, fontSize: 17, fontWeight: '800', textAlign: 'center', marginTop: 5 },
  alertMessage: { color: colors.onPrimaryMuted, fontSize: 14, lineHeight: 21, textAlign: 'center', marginTop: 16 },
  alertAcknowledgeButton: { minHeight: 56, width: '100%', borderRadius: 10, backgroundColor: colors.surface, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10, marginTop: 36, paddingHorizontal: 14 },
  alertAcknowledgeText: { color: colors.greenDark, fontSize: 11, fontWeight: '900', letterSpacing: 0.4 },
  });
}

function useAppTheme(): AppTheme {
  const theme = useContext(ThemeContext);
  if (!theme) throw new Error('ThemeContext provider is missing.');
  return theme;
}
