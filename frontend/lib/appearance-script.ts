// Pre-hydration script: runs synchronously in <head> before React mounts.
// Reads the mizan-appearance cookie, falls back to prefers-color-scheme,
// and sets classes on <html> so the first paint never flashes.
//
// SYNC: cookie key must match APPEARANCE_COOKIE in lib/appearance-cookie.ts.

export const APPEARANCE_SCRIPT = `(function(){try{var c=document.cookie.match(/(?:^|; )mizan-appearance=([^;]*)/);var p=c?JSON.parse(decodeURIComponent(c[1])):null;var t=p&&(p.theme==="light"||p.theme==="dark"||p.theme==="system")?p.theme:"system";var eff=t==="system"?(window.matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light"):t;var r=document.documentElement;r.classList.toggle("dark",eff==="dark");if(p){r.classList.toggle("compact",!!p.compactMode);r.classList.toggle("reduce-motion",!!p.reduceAnimations);}}catch(e){}})();`;
