# Recurrence Rules

- Once, Daily (1–365), Weekly (weekdays + interval), Monthly (1–31 + ClampToLastDay|SkipOccurrence), CustomDates (≤100 unique sorted)
- OccurrenceKey: `{campaignId:N}:{yyyyMMddHHmm}|{timeZoneId}`
- Default TZ Asia/Riyadh; engine tested with America/New_York DST
- Windows: Open + ResponseWindow; Grace; CloseAfter
- BusinessDayAdjustment: None|Next|Previous (Fri/Sat weekend + org calendar overrides)
