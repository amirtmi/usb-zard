# Validation / اعتبارسنجی

The application was built on Windows x64 with .NET SDK 8.0.425.

- 13 diagnosis and error-description assertions.
- 9 mocked safety rejection scenarios plus valid-request checks.
- 18 mocked worker assertions, including missing consent, RAW CHKDSK rejection, unsupported attributes and false format-success prevention.
- Persian/English offscreen WPF renders and action-gating checks.

برنامه در Windows x64 با .NET SDK 8.0.425 ساخته شد. آزمون‌ها شامل ۱۳ بررسی تشخیص و توضیح خطا، ۹ سناریوی رد عملیات ناامن، ۱۸ بررسی اجرای شبیه‌سازی‌شده فرمان‌ها و بررسی رابط فارسی/انگلیسی هستند.

Limited read-only checks of the actual backend have also been performed on physical USB media. They do not certify repair capability or every device. A problematic device can remain RAW and unrepairable using standard Windows operations.

بررسی محدود فقط‌خواندنیِ کد واقعی روی رسانه USB نیز انجام شده است. این بررسی‌ها تضمین تعمیر یا پشتیبانی همه دستگاه‌ها نیستند؛ یک دستگاه معیوب ممکن است پس از روش‌های استاندارد ویندوز همچنان RAW باقی بماند.

No hardware repair success or universal recovery rate is claimed. A successful build, a successful command and a successfully repaired storage device are different outcomes.

ساخت موفق، اجرای موفق فرمان و تعمیر موفق دستگاه سه نتیجه متفاوت هستند. تضمین تعمیر سخت‌افزار یا نرخ بازیابی همگانی ارائه نمی‌شود.
