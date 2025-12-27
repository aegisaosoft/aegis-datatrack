import { useState, useEffect } from 'react';

const API_BASE = '/api';

export default function ExternalCompanies() {
  const [rentalCompanies, setRentalCompanies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  
  // Modal state
  const [showModal, setShowModal] = useState(false);
  const [selectedCompany, setSelectedCompany] = useState(null);
  const [formData, setFormData] = useState({
    trackerProvider: 'Datatrack 247',
    apiBaseUrl: 'https://fm.datatrack247.com/api',
    apiUsername: '',
    apiPassword: ''
  });
  
  const [actionLoading, setActionLoading] = useState({});

  useEffect(() => {
    fetchRentalCompanies();
  }, []);

  const fetchRentalCompanies = async () => {
    try {
      setLoading(true);
      const res = await fetch(`${API_BASE}/external/rental-companies-with-tracker`);
      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || `Failed to fetch companies (${res.status})`);
      }
      const data = await res.json();
      setRentalCompanies(data);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const openTrackerModal = (company) => {
    setSelectedCompany(company);
    setFormData({
      trackerProvider: company.trackerProvider || 'Datatrack 247',
      apiBaseUrl: company.apiBaseUrl || 'https://fm.datatrack247.com/api',
      apiUsername: company.trackerUsername || '',
      apiPassword: ''
    });
    setShowModal(true);
  };

  const handleSaveTracker = async (e) => {
    e.preventDefault();
    if (!selectedCompany) return;

    try {
      setActionLoading(prev => ({ ...prev, save: true }));
      
      const res = await fetch(`${API_BASE}/external/setup-tracker`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          rentalCompanyId: selectedCompany.id,
          trackerProvider: formData.trackerProvider,
          apiBaseUrl: formData.apiBaseUrl,
          apiUsername: formData.apiUsername,
          apiPassword: formData.apiPassword
        })
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || 'Failed to setup tracker');
      }

      setSuccess(`Tracker configured for ${selectedCompany.companyName}`);
      setShowModal(false);
      fetchRentalCompanies();
      
      setTimeout(() => setSuccess(null), 3000);
    } catch (err) {
      setError(err.message);
    } finally {
      setActionLoading(prev => ({ ...prev, save: false }));
    }
  };

  const handleLogin = async (company) => {
    if (!company.externalCompanyId) {
      setError('Please configure tracker credentials first');
      return;
    }

    try {
      setActionLoading(prev => ({ ...prev, [company.id]: 'login' }));
      
      const res = await fetch(`${API_BASE}/external/companies/${company.externalCompanyId}/login`, {
        method: 'POST'
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || 'Login failed');
      }

      const data = await res.json();
      setSuccess(`Login successful! Token valid until ${new Date(data.expiresAt).toLocaleString()}`);
      fetchRentalCompanies();
      
      setTimeout(() => setSuccess(null), 3000);
    } catch (err) {
      setError(err.message);
    } finally {
      setActionLoading(prev => ({ ...prev, [company.id]: null }));
    }
  };

  const handleSync = async (company) => {
    if (!company.externalCompanyId) {
      setError('Please configure tracker credentials first');
      return;
    }

    try {
      setActionLoading(prev => ({ ...prev, [company.id]: 'sync' }));
      
      const res = await fetch(`${API_BASE}/external/companies/${company.externalCompanyId}/sync`, {
        method: 'POST'
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || 'Sync failed');
      }

      const data = await res.json();
      setSuccess(`${company.companyName}: ${data.created} new, ${data.updated} updated (${data.vehicleCount} total)`);
      fetchRentalCompanies();
      
      setTimeout(() => setSuccess(null), 5000);
    } catch (err) {
      setError(err.message);
    } finally {
      setActionLoading(prev => ({ ...prev, [company.id]: null }));
    }
  };

  if (loading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Tracker Configuration</h1>
        <p className="text-gray-600 mt-1">Configure GPS tracking for your rental companies</p>
      </div>

      {error && (
        <div className="mb-4 bg-red-100 border-l-4 border-red-500 text-red-700 p-4 rounded">
          <div className="flex justify-between">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-700 hover:text-red-900">✕</button>
          </div>
        </div>
      )}

      {success && (
        <div className="mb-4 bg-green-100 border-l-4 border-green-500 text-green-700 p-4 rounded">
          <div className="flex justify-between">
            <span>{success}</span>
            <button onClick={() => setSuccess(null)} className="text-green-700 hover:text-green-900">✕</button>
          </div>
        </div>
      )}

      {rentalCompanies.length === 0 ? (
        <div className="bg-white rounded-lg shadow p-8 text-center">
          <p className="text-gray-600">No rental companies found in the database.</p>
          <p className="text-sm text-gray-500 mt-2">Add companies to the `companies` table first.</p>
        </div>
      ) : (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Rental Company
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Tracker Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Vehicles
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {rentalCompanies.map(company => (
                <tr key={company.id} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="font-medium text-gray-900">{company.companyName}</div>
                    <div className="text-sm text-gray-500">{company.email}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    {company.externalCompanyId ? (
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="text-sm text-gray-600">{company.trackerProvider}</span>
                          {company.tokenValid ? (
                            <span className="px-2 py-1 text-xs rounded-full bg-green-100 text-green-700">
                              ✓ Connected
                            </span>
                          ) : company.hasToken ? (
                            <span className="px-2 py-1 text-xs rounded-full bg-yellow-100 text-yellow-700">
                              Token Expired
                            </span>
                          ) : (
                            <span className="px-2 py-1 text-xs rounded-full bg-gray-100 text-gray-700">
                              Not Logged In
                            </span>
                          )}
                        </div>
                        <div className="text-xs text-gray-500 mt-1">
                          User: {company.trackerUsername}
                        </div>
                      </div>
                    ) : (
                      <span className="text-sm text-gray-400 italic">Not configured</span>
                    )}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className="text-sm text-gray-600">
                      {company.vehicleCount || 0} synced
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right">
                    <div className="flex justify-end gap-2">
                      <button
                        onClick={() => openTrackerModal(company)}
                        className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
                      >
                        ⚙️ {company.externalCompanyId ? 'Edit' : 'Configure'}
                      </button>
                      
                      {company.externalCompanyId && (
                        <>
                          <button
                            onClick={() => handleLogin(company)}
                            disabled={actionLoading[company.id] === 'login'}
                            className="px-3 py-1.5 text-sm bg-green-600 text-white rounded hover:bg-green-700 transition-colors disabled:opacity-50"
                          >
                            {actionLoading[company.id] === 'login' ? '...' : '🔐 Login'}
                          </button>
                          
                          <button
                            onClick={() => handleSync(company)}
                            disabled={!company.tokenValid || actionLoading[company.id] === 'sync'}
                            className="px-3 py-1.5 text-sm bg-purple-600 text-white rounded hover:bg-purple-700 transition-colors disabled:opacity-50"
                          >
                            {actionLoading[company.id] === 'sync' ? '...' : '🔄 Sync'}
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Configure Tracker Modal */}
      {showModal && selectedCompany && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl w-full max-w-md mx-4">
            <div className="p-6">
              <h2 className="text-xl font-bold text-gray-900 mb-4">
                Configure Tracker for {selectedCompany.companyName}
              </h2>
              
              <form onSubmit={handleSaveTracker}>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Tracker Provider
                    </label>
                    <select
                      value={formData.trackerProvider}
                      onChange={(e) => {
                        const provider = e.target.value;
                        let url = formData.apiBaseUrl;
                        if (provider === 'Datatrack 247') url = 'https://fm.datatrack247.com/api';
                        else if (provider === 'CalAmp') url = 'https://api.calamp.com';
                        setFormData({ ...formData, trackerProvider: provider, apiBaseUrl: url });
                      }}
                      className="w-full border rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    >
                      <option value="Datatrack 247">Datatrack 247</option>
                      <option value="CalAmp">CalAmp</option>
                      <option value="Other">Other</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      API Base URL
                    </label>
                    <input
                      type="url"
                      value={formData.apiBaseUrl}
                      onChange={(e) => setFormData({ ...formData, apiBaseUrl: e.target.value })}
                      className="w-full border rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                      placeholder="https://fm.datatrack247.com/api"
                      required
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Username
                    </label>
                    <input
                      type="text"
                      value={formData.apiUsername}
                      onChange={(e) => setFormData({ ...formData, apiUsername: e.target.value })}
                      className="w-full border rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                      placeholder="Tracker username"
                      required
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Password
                    </label>
                    <input
                      type="password"
                      value={formData.apiPassword}
                      onChange={(e) => setFormData({ ...formData, apiPassword: e.target.value })}
                      className="w-full border rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                      placeholder={selectedCompany.externalCompanyId ? "(leave empty to keep current)" : "Tracker password"}
                      required={!selectedCompany.externalCompanyId}
                    />
                  </div>
                </div>

                <div className="mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setShowModal(false)}
                    className="px-4 py-2 text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={actionLoading.save}
                    className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
                  >
                    {actionLoading.save ? 'Saving...' : 'Save'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
