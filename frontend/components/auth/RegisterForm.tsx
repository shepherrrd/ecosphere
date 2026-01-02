"use client";

import { useState } from "react";
import { useAuth } from "@/lib/contexts/AuthContext";

export default function RegisterForm({ onSwitchToLogin }: { onSwitchToLogin: () => void }) {
  const { register } = useAuth();
  const [formData, setFormData] = useState({
    userName: "",
    email: "",
    password: "",
    confirmPassword: "",
    displayName: "",
  });
  const [errors, setErrors] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const validatePassword = (password: string): string[] => {
    const validationErrors: string[] = [];

    if (password.length < 8) {
      validationErrors.push("Password must be at least 8 characters long");
    }

    if (!/[A-Z]/.test(password)) {
      validationErrors.push("Password must contain at least one uppercase letter");
    }

    if (!/[a-z]/.test(password)) {
      validationErrors.push("Password must contain at least one lowercase letter");
    }

    if (!/[0-9]/.test(password)) {
      validationErrors.push("Password must contain at least one number");
    }

    if (!/[!@#$%&^]/.test(password)) {
      validationErrors.push("Password must contain at least one special character (!@#$%&^)");
    }

    return validationErrors;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);

    const validationErrors: string[] = [];

    // Validate password
    const passwordErrors = validatePassword(formData.password);
    validationErrors.push(...passwordErrors);

    // Check password confirmation
    if (formData.password !== formData.confirmPassword) {
      validationErrors.push("Passwords do not match");
    }

    if (validationErrors.length > 0) {
      setErrors(validationErrors);
      return;
    }

    setIsLoading(true);

    try {
      const result = await register({
        userName: formData.userName,
        email: formData.email,
        password: formData.password,
        displayName: formData.displayName,
        deviceToken: navigator.userAgent,
        deviceName: navigator.userAgent.substring(0, 50),
        deviceType: /Mobile|Android|iPhone/i.test(navigator.userAgent) ? "Mobile" : "Desktop",
      });

      if (!result.success) {
        if (Array.isArray(result.errors)) {
          setErrors(result.errors);
        } else if (result.message) {
          setErrors([result.message]);
        } else {
          setErrors(["Registration failed. Please try again."]);
        }
      }
    } catch (err) {
      setErrors(["An unexpected error occurred"]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  return (
    <div className="w-full max-w-md p-8 bg-gray-900 rounded-lg border border-gray-800">
      <h2 className="text-3xl font-bold text-center mb-6 text-white">
        Join Ecosphere
      </h2>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="displayName" className="block text-sm font-medium text-gray-300 mb-1">
            Display Name
          </label>
          <input
            id="displayName"
            name="displayName"
            type="text"
            value={formData.displayName}
            onChange={handleChange}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="John Doe"
          />
        </div>

        <div>
          <label htmlFor="userName" className="block text-sm font-medium text-gray-300 mb-1">
            Username
          </label>
          <input
            id="userName"
            name="userName"
            type="text"
            value={formData.userName}
            onChange={handleChange}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="johndoe"
          />
        </div>

        <div>
          <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-1">
            Email
          </label>
          <input
            id="email"
            name="email"
            type="email"
            value={formData.email}
            onChange={handleChange}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="your@email.com"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-gray-300 mb-1">
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            value={formData.password}
            onChange={handleChange}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="••••••••"
          />
        </div>

        <div>
          <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-300 mb-1">
            Confirm Password
          </label>
          <input
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            value={formData.confirmPassword}
            onChange={handleChange}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="••••••••"
          />
        </div>

        {errors.length > 0 && (
          <div className="p-3 bg-red-900 border border-red-700 text-red-200 rounded-md text-sm">
            {errors.length === 1 ? (
              <p>{errors[0]}</p>
            ) : (
              <ul className="list-disc list-inside space-y-1">
                {errors.map((error, index) => (
                  <li key={index}>{error}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        <button
          type="submit"
          disabled={isLoading}
          className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 disabled:cursor-not-allowed text-white font-medium rounded-md transition-colors duration-200"
        >
          {isLoading ? "Creating account..." : "Create Account"}
        </button>
      </form>

      <div className="mt-6 text-center">
        <p className="text-sm text-gray-400">
          Already have an account?{" "}
          <button
            onClick={onSwitchToLogin}
            className="text-blue-400 hover:underline font-medium"
          >
            Sign in
          </button>
        </p>
      </div>
    </div>
  );
}
