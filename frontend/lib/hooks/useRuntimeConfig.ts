'use client';

import { useEffect, useState } from 'react';

interface RuntimeConfig {
  cloudinary: {
    cloudName: string;
    apiKey: string;
  };
  appUrl: string;
  apiUrl: string;
}

let cachedConfig: RuntimeConfig | null = null;

export function useRuntimeConfig() {
  const [config, setConfig] = useState<RuntimeConfig | null>(cachedConfig);
  const [loading, setLoading] = useState(!cachedConfig);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (cachedConfig) {
      setConfig(cachedConfig);
      setLoading(false);
      return;
    }

    fetch('/api/config')
      .then(res => {
        if (!res.ok) throw new Error('Failed to fetch runtime config');
        return res.json();
      })
      .then(data => {
        cachedConfig = data;
        setConfig(data);
        setLoading(false);
      })
      .catch(err => {
        setError(err);
        setLoading(false);
      });
  }, []);

  return { config, loading, error };
}
