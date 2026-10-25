"use client";

import { useEffect, useState } from "react";
import {
  getMyProfile,
  listPriceRegions,
  setPriceRegion,
  type PriceRegion,
} from "@/lib/api/prices";
import { appToast } from "@/lib/toast";

/**
 * Which region's prices cost the person's plans and lists. The currency
 * comes from the region; a region without prices shows no cost at all.
 */
export default function PriceRegionField() {
  const [regions, setRegions] = useState<PriceRegion[] | null>(null);
  const [value, setValue] = useState("");
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    Promise.all([listPriceRegions(), getMyProfile()])
      .then(([list, profile]) => {
        if (!active) return;
        setRegions(list);
        setValue(profile.priceRegionCode ?? "ET-AA");
      })
      .catch(() => {
        if (active) setFailed(true);
      });
    return () => {
      active = false;
    };
  }, []);

  async function save(next: string) {
    const previous = value;
    setValue(next);
    setSaving(true);
    try {
      await setPriceRegion(next);
      const region = regions?.find((item) => item.code === next);
      appToast.success(
        region
          ? `Prices now come from ${region.name}, in ${region.currency}`
          : "Region saved",
      );
    } catch (error) {
      setValue(previous);
      appToast.error(error, "Could not save your region");
    } finally {
      setSaving(false);
    }
  }

  if (failed)
    return <p className="text-sm log-muted">Regions could not be loaded.</p>;

  return (
    <div className="space-y-2">
      <label htmlFor="price-region" className="label">
        Price region
      </label>
      <select
        id="price-region"
        value={value}
        disabled={saving || regions === null}
        onChange={(event) => save(event.target.value)}
        className="input w-full"
      >
        {regions === null ? (
          <option value="">Loading regions</option>
        ) : (
          regions.map((region) => (
            <option key={region.code} value={region.code}>
              {region.name} ({region.currency})
            </option>
          ))
        )}
      </select>
      <p className="text-xs log-muted">
        Plans and shopping lists are costed with this region&apos;s prices.
      </p>
    </div>
  );
}
