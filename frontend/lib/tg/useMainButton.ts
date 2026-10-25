"use client";

import { useEffect, useRef } from "react";
import { mainButton } from "./webApp";

export type MainButtonAction = {
  text: string;
  /** Shown greyed out while true. */
  disabled?: boolean;
  /** Shows Telegram's spinner while the write runs. */
  busy?: boolean;
  onClick: () => void;
};

/**
 * The page's primary action on Telegram's main button. Null hides the button.
 * Outside Telegram this does nothing, and the page's own button stays the way
 * to act; inside, both work. The button is hidden when the page goes away.
 */
export function useMainButton(action: MainButtonAction | null) {
  const latest = useRef(action);
  useEffect(() => {
    latest.current = action;
  });

  const text = action?.text;
  const enabled = action ? !action.disabled && !action.busy : false;
  const busy = action?.busy ?? false;
  useEffect(() => {
    const button = mainButton();
    if (!button || text === undefined) return;
    button.setText(text);
    button.setEnabled(enabled);
    button.showProgress(busy);
  }, [text, enabled, busy]);

  const shown = action !== null;
  useEffect(() => {
    const button = mainButton();
    if (!button || !shown) return;
    const stop = button.onClick(() => {
      const current = latest.current;
      if (current && !current.disabled && !current.busy) current.onClick();
    });
    button.show();
    return () => {
      stop();
      button.hide();
    };
  }, [shown]);
}
