"use client";

import Link from "next/link";
import { useState, type ComponentProps } from "react";

/**
 * A link whose whole destination is fetched once the person points at it, focuses
 * it or touches it, rather than only its loading state. A title shared with the
 * page it opens can only travel when that page renders at once, so links into an
 * item use this. Prefetching runs in production builds only.
 */
export default function IntentLink(props: ComponentProps<typeof Link>) {
  const [intent, setIntent] = useState(false);
  const want = () => setIntent(true);
  return (
    <Link
      {...props}
      prefetch={intent ? true : props.prefetch}
      onMouseEnter={(event) => {
        want();
        props.onMouseEnter?.(event);
      }}
      onFocus={(event) => {
        want();
        props.onFocus?.(event);
      }}
      onTouchStart={(event) => {
        want();
        props.onTouchStart?.(event);
      }}
    />
  );
}
