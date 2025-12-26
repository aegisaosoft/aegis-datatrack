import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor for logging
api.interceptors.request.use(
  (config) => {
    console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`);
    return config;
  },
  (error) => {
    console.error('[API] Request error:', error);
    return Promise.reject(error);
  }
);

// Response interceptor for error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('[API] Response error:', error.response?.data || error.message);
    return Promise.reject(error);
  }
);

export const vehicleApi = {
  // Get all vehicle statuses with current locations
  getAllStatuses: async () => {
    const response = await api.get('/vehicles/statuses');
    return response.data;
  },

  // Get single vehicle status
  getStatus: async (serial) => {
    const response = await api.get(`/vehicles/statuses/${serial}`);
    return response.data;
  },

  // Get all vehicles
  getAllVehicles: async () => {
    const response = await api.get('/vehicles');
    return response.data;
  },

  // Get single vehicle details
  getVehicle: async (serial) => {
    const response = await api.get(`/vehicles/${serial}`);
    return response.data;
  },

  // Get location history for a vehicle
  getLocations: async (serial, options = {}) => {
    const params = new URLSearchParams();
    if (options.start) params.append('start', options.start);
    if (options.end) params.append('end', options.end);
    if (options.hoursBack) params.append('hoursBack', options.hoursBack);
    
    const response = await api.get(`/vehicles/${serial}/locations?${params}`);
    return response.data;
  },

  // Control starter
  setStarter: async (serial, disable) => {
    const response = await api.post(`/vehicles/${serial}/starter`, { disable });
    return response.data;
  },

  // Control buzzer
  setBuzzer: async (serial, disable) => {
    const response = await api.post(`/vehicles/${serial}/buzzer`, { disable });
    return response.data;
  },
};

export default api;
