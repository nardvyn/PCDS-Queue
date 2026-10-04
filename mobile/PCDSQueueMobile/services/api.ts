import AsyncStorage from '@react-native-async-storage/async-storage';

const API_URL_STORAGE_KEY = 'pcds.apiBaseUrl';
// Android devices on the same Wi-Fi network reach the Flask PC through its LAN IP.
// `10.0.2.2` works only from an Android emulator, never from a physical phone.
const DEFAULT_API_BASE_URL = process.env.EXPO_PUBLIC_API_URL || 'http://192.168.254.102:5000';

let apiBaseUrl = normalizeBaseUrl(DEFAULT_API_BASE_URL);

export type Department = {
  department_id: number;
  department_name: string;
  queue_prefix: string;
  queue_is_open: boolean;
};

export type ValidatedQr = {
  valid: boolean;
  session_id: number;
  department: Department;
  expires_at: string;
  mobile_queue_enabled: boolean;
};

export type QueueTicket = {
  queue_id: number;
  queue_number: string;
  sequence_number: number;
  department: {
    department_id: number;
    department_name: string;
    queue_prefix: string;
  };
  source: string;
  status: string;
  people_ahead: number;
};

export type QueueStatus = {
  queue_id: number;
  queue_number: string;
  department: string;
  status: string;
  people_ahead: number;
  estimated_wait_minutes: number;
  current_serving: string | null;
  window_number: number | null;
  window_name: string | null;
  called_at: string | null;
};

export type DepartmentQueueStatus = {
  waiting_count: number;
  active_window_count: number;
  serving: {
    queue_number: string;
    window_number: number | null;
    window_name: string | null;
  }[];
};
export type QueueAnnouncementEvent = {
  event_id: number;
  queue_id: number;
  event_type: 'CALLED' | 'RECALLED';
  queue_number: string;
  department_name: string;
  window_number: number | null;
  window_name: string | null;
  created_at: string;
};

export type QueueAnnouncementEvents = {
  events: QueueAnnouncementEvent[];
  latest_event_id: number;
  next_after_event_id: number;
};


export class ApiError extends Error {
  constructor(
    message: string,
    readonly statusCode: number,
    readonly payload: unknown,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function initializeApiUrl(): Promise<string> {
  const savedUrl = await AsyncStorage.getItem(API_URL_STORAGE_KEY);
  const usableSavedUrl = savedUrl?.includes('10.0.2.2') ? null : savedUrl;
  apiBaseUrl = normalizeBaseUrl(usableSavedUrl || DEFAULT_API_BASE_URL);
  return apiBaseUrl;
}

export async function saveApiUrl(value: string): Promise<string> {
  apiBaseUrl = normalizeBaseUrl(value);
  await AsyncStorage.setItem(API_URL_STORAGE_KEY, apiBaseUrl);
  return apiBaseUrl;
}

export function getApiUrl(): string {
  return apiBaseUrl;
}

export async function checkApiHealth(): Promise<void> {
  await request<{ status: string }>('api/health');
}

export async function validateQrToken(token: string): Promise<ValidatedQr> {
  return request<ValidatedQr>(`api/qr/validate/${encodeURIComponent(token)}`);
}

export async function generateMobileQueue(
  departmentId: number,
  deviceIdentifier: string,
  notificationToken?: string,
): Promise<QueueTicket> {
  const response = await request<{ queue: QueueTicket }>('api/queue/generate', {
    method: 'POST',
    body: JSON.stringify({
      department_id: departmentId,
      source: 'MOBILE',
      device_identifier: deviceIdentifier,
      notification_token: notificationToken,
    }),
  });
  return response.queue;
}

export async function getQueueStatus(queueId: number): Promise<QueueStatus> {
  return request<QueueStatus>(`api/queue/${queueId}/status`);
}
export async function getQueueAnnouncementEvents(
  queueId: number,
  deviceIdentifier: string,
  afterEventId: number,
): Promise<QueueAnnouncementEvents> {
  const query = new URLSearchParams({
    queue_id: String(queueId),
    device_identifier: deviceIdentifier,
    after_event_id: String(afterEventId),
  });
  return request<QueueAnnouncementEvents>(`api/queue/announcement-events?${query.toString()}`);
}

export async function getDepartmentQueueStatus(departmentId: number): Promise<DepartmentQueueStatus> {
  return request<DepartmentQueueStatus>(`api/queue/status/${departmentId}`);
}

export async function cancelMobileQueue(
  queueId: number,
  deviceIdentifier: string,
): Promise<void> {
  await request(`api/queue/${queueId}/cancel`, {
    method: 'POST',
    body: JSON.stringify({ device_identifier: deviceIdentifier }),
  });
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);

  try {
    const response = await fetch(`${apiBaseUrl}/${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...init?.headers,
      },
      signal: controller.signal,
    });
    const payload = await response.json().catch(() => ({}));

    if (!response.ok) {
      const message =
        typeof payload?.message === 'string'
          ? payload.message
          : 'The queue server could not complete the request.';
      throw new ApiError(message, response.status, payload);
    }

    return payload as T;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }
    if (error instanceof Error && error.name === 'AbortError') {
      throw new Error('The server took too long to respond. Check your connection and try again.');
    }
    throw new Error('Cannot connect to the PCDS Queue server. Check the API address and network.');
  } finally {
    clearTimeout(timeout);
  }
}

function normalizeBaseUrl(value: string): string {
  const trimmed = value.trim();
  if (!/^https?:\/\//i.test(trimmed)) {
    throw new Error('API address must start with http:// or https://.');
  }
  return trimmed.replace(/\/+$/, '');
}
