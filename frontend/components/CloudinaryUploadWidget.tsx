'use client';

import { CldUploadWidget } from 'next-cloudinary';
import type { CloudinaryUploadWidgetResults } from 'next-cloudinary';
import { useRuntimeConfig } from '@/lib/hooks/useRuntimeConfig';

interface CloudinaryUploadWidgetProps {
  onSuccess: (result: CloudinaryUploadWidgetResults) => void;
  signatureEndpoint?: string;
  uploadPreset?: string;
  children: (props: { open: () => void }) => React.ReactNode;
}

export function CloudinaryUploadWidget({
  onSuccess,
  signatureEndpoint,
  uploadPreset,
  children,
}: CloudinaryUploadWidgetProps) {
  const { config, loading, error } = useRuntimeConfig();

  if (loading) {
    return (
      <div className="w-24 h-24 rounded-2xl border-2 border-dashed border-slate-300 bg-slate-50 flex flex-col items-center justify-center">
        <i className="ri-loader-4-line text-2xl text-slate-400 animate-spin" />
        <span className="text-xs text-slate-400 mt-1">Loading...</span>
      </div>
    );
  }

  if (error || !config?.cloudinary?.cloudName) {
    return (
      <div className="w-24 h-24 rounded-2xl border-2 border-dashed border-red-300 bg-red-50 flex flex-col items-center justify-center">
        <i className="ri-error-warning-line text-2xl text-red-400" />
        <span className="text-xs text-red-400 mt-1">Error</span>
      </div>
    );
  }

  return (
    <CldUploadWidget
      onSuccess={onSuccess}
      signatureEndpoint={signatureEndpoint}
      uploadPreset={uploadPreset}
      options={{ cloudName: config.cloudinary.cloudName }}
    >
      {children}
    </CldUploadWidget>
  );
}
