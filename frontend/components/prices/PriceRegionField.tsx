"use client";

import { useEffect, useState } from "react";
import {
  getMyProfile,
  listPriceRegions,
  setPriceRegion,
  type PriceRegion,
} from "@/lib/api/prices";
import { appToast } from "@/lib/toast";
import { Select } from "@/components/ui/select";

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
    return (
      <div id="prices" className="setting-row setting-section">
        <span className="setting-label">Price region</span>
        <p className="text-sm log-muted">Regions could not be loaded.</p>
      </div>
    );

  return (
    <div id="prices" className="setting-row setting-section">
      <label htmlFor="price-region" className="setting-label">
        Price region
      </label>
      <div className="space-y-2">
        <Select
          id="price-region"
          searchable
          value={value}
          disabled={saving || regions === null}
          onChange={save}
          placeholder="Loading regions"
          options={(regions ?? []).map((region) => ({
            value: region.code,
            label: region.name,
            hint: region.currency,
          }))}
        />
        <p className="text-xs log-muted">
          Where you shop. Plans and shopping lists are costed with this
          region&apos;s prices.
        </p>
      </div>
    </div>
  );
}
