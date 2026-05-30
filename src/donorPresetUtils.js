export function keyFromPair(restaurantName, orderUrl) {
  const r = String(restaurantName ?? "").trim();
  const u = String(orderUrl ?? "").trim();
  return `${r}::${u}`;
}

export function keyForPreset(preset) {
  return keyFromPair(preset.restaurant_name, preset.order_url);
}

export function normalizePresetsForStorage(userId, presets) {
  const now = new Date().toISOString();
  const deduped = new Map();
  for (const preset of presets) {
    const normalized = {
      id:
        typeof preset.id === "string" && preset.id.trim().length > 0
          ? preset.id.trim()
          : `${userId}-preset-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`,
      restaurant_name: preset.restaurant_name,
      order_url: preset.order_url,
      menu_items: preset.menu_items,
      app_name: preset.app_name,
      source: preset.source,
      confidence: preset.confidence,
      saved_at:
        typeof preset.saved_at === "string" && preset.saved_at.trim().length > 0
          ? preset.saved_at
          : now
    };
    deduped.set(keyForPreset(normalized), normalized);
  }
  return [...deduped.values()];
}
