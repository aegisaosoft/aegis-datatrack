import { useState } from 'react';
import { Eye, EyeOff, Truck, Key, User } from 'lucide-react';

export default function CredentialsModal({ 
  company, 
  onSubmit, 
  onCancel,
  isLoading,
  error 
}) {
  const [form, setForm] = useState({
    apiUsername: '',
    apiPassword: ''
  });
  const [showPassword, setShowPassword] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    onSubmit(form);
  };

  if (!company) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md mx-4">
        <div className="p-6">
          {/* Header */}
          <div className="flex items-center gap-3 mb-6">
            <div className="p-3 bg-blue-100 rounded-full">
              <Truck className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-gray-900">
                Connect GPS Tracker
              </h2>
              <p className="text-sm text-gray-500">{company.companyName}</p>
            </div>
          </div>
          
          <form onSubmit={handleSubmit}>
            <div className="space-y-4">
              {/* Info box */}
              <div className="bg-blue-50 p-4 rounded-lg">
                <p className="text-sm text-blue-800">
                  Enter your Fleet77 / DataTrack247 login credentials.
                </p>
              </div>

              {/* Username */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Username
                </label>
                <div className="relative">
                  <User className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <input
                    type="text"
                    value={form.apiUsername}
                    onChange={(e) => setForm({ ...form, apiUsername: e.target.value })}
                    className="w-full pl-10 pr-4 py-3 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="Enter your username"
                    autoComplete="username"
                    required
                  />
                </div>
              </div>

              {/* Password */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Password
                </label>
                <div className="relative">
                  <Key className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    value={form.apiPassword}
                    onChange={(e) => setForm({ ...form, apiPassword: e.target.value })}
                    className="w-full pl-10 pr-12 py-3 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="Enter your password"
                    autoComplete="current-password"
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                  </button>
                </div>
              </div>
            </div>

            {/* Error message */}
            {error && (
              <div className="mt-4 p-3 bg-red-100 text-red-700 rounded-lg text-sm">
                ⚠️ {error}
              </div>
            )}

            {/* Buttons */}
            <div className="mt-6 flex gap-3">
              <button
                type="button"
                onClick={onCancel}
                className="flex-1 px-4 py-3 text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors font-medium"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isLoading || !form.apiUsername || !form.apiPassword}
                className="flex-1 px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
              >
                {isLoading ? (
                  <>
                    <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    Connecting...
                  </>
                ) : (
                  'Connect'
                )}
              </button>
            </div>
          </form>

          {/* Footer */}
          <p className="mt-4 text-xs text-gray-500 text-center">
            Using Fleet77 / DataTrack247 API
          </p>
        </div>
      </div>
    </div>
  );
}
