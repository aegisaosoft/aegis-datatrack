import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Store for active company ID
let activeCompanyId = localStorage.getItem('activeCompanyId') || null;

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

// Helper to add companyId to params
const withCompany = (params = {}) => {
  if (activeCompanyId) {
    params.companyId = activeCompanyId;
  }
  return params;
};

export const companyApi = {
  // Get all external companies
  getCompanies: async () => {
    const response = await api.get('/external/companies');
    return response.data;
  },

  // Create company
  createCompany: async (data) => {
    const response = await api.post('/external/companies', data);
    return response.data;
  },

  // Update company
  updateCompany: async (id, data) => {
    const response = await api.put(`/external/companies/${id}`, data);
    return response.data;
  },

  // Delete company
  deleteCompany: async (id) => {
    await api.delete(`/external/companies/${id}`);
  },

  // Login to company
  login: async (id) => {
    const response = await api.post(`/external/companies/${id}/login`);
    return response.data;
  },

  // Sync vehicles from company
  syncVehicles: async (id) => {
    const response = await api.post(`/external/companies/${id}/sync`);
    return response.data;
  },

  // Set active company (for vehicle API calls)
  setActiveCompany: (id) => {
    activeCompanyId = id;
    if (id) {
      localStorage.setItem('activeCompanyId', id);
    } else {
      localStorage.removeItem('activeCompanyId');
    }
  },

  // Clear active company (logout)
  clearActiveCompany: () => {
    activeCompanyId = null;
    localStorage.removeItem('activeCompanyId');
  },

  // Get active company ID
  getActiveCompanyId: () => activeCompanyId,

  // Get rental companies (for linking dropdown)
  getRentalCompanies: async () => {
    const response = await api.get('/external/rental-companies');
    return response.data;
  },
};

export const vehicleApi = {
  // Get all vehicle statuses with current locations
  getAllStatuses: async () => {
    const response = await api.get('/vehicles/statuses', { params: withCompany() });
    return response.data;
  },

  // Get single vehicle status
  getStatus: async (serial) => {
    const response = await api.get(`/vehicles/statuses/${serial}`, { params: withCompany() });
    return response.data;
  },

  // Get all vehicles
  getAllVehicles: async () => {
    const response = await api.get('/vehicles', { params: withCompany() });
    return response.data;
  },

  // Get single vehicle details
  getVehicle: async (serial) => {
    const response = await api.get(`/vehicles/${serial}`, { params: withCompany() });
    return response.data;
  },

  // Get location history for a vehicle
  getLocations: async (serial, options = {}) => {
    const params = withCompany();
    if (options.start) params.start = options.start;
    if (options.end) params.end = options.end;
    if (options.hoursBack) params.hoursBack = options.hoursBack;
    
    const response = await api.get(`/vehicles/${serial}/locations`, { params });
    return response.data;
  },

  // Control starter
  setStarter: async (serial, disable) => {
    const response = await api.post(`/vehicles/${serial}/starter`, { disable }, { params: withCompany() });
    return response.data;
  },

  // Control buzzer
  setBuzzer: async (serial, disable) => {
    const response = await api.post(`/vehicles/${serial}/buzzer`, { disable }, { params: withCompany() });
    return response.data;
  },
};

export default api;
