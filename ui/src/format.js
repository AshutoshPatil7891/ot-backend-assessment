// Small helpers shared by the pages.

// 123455 -> "123,455.00"
export function formatMoney(value) {
  return Number(value).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

// "smoke5128" -> "S" (shown in the round avatar)
export function initialOf(name) {
  return (name || '?').charAt(0).toUpperCase();
}
