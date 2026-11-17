$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5199/api'

# Local educational samples are based on questions 1–6 of the supplied PDF.
# Their company names and interview narratives are illustrative, not user reports.
$samples = @(
    @{ company = 'نمونهٔ آموزشی: تیم API'; level = 'Mid'; question = 'تفاوت Thread، Task و async/await چیست و چه زمانی async واقعاً مقیاس‌پذیری را بهتر می‌کند؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۱ PDF: در یک سناریوی I/O-bound توضیح داده شد چرا انتظار غیرهم‌زمان، Thread درخواست را آزاد می‌کند و چرا Task.Run راه‌حل عمومی برای I/O نیست.'; stage = 'گفت‌وگو دربارهٔ Thread Pool، عملیات I/O و تفاوت async با اجرای موازی.' }
    @{ company = 'نمونهٔ آموزشی: تیم پلتفرم'; level = 'Senior'; question = 'Captive Dependency چیست و Lifetimeهای DI در ASP.NET Core چه تفاوتی دارند؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۲ PDF: دربارهٔ تزریق DbContext با Lifetime نوع Scoped داخل Singleton و پیامدهای آن برای دادهٔ کهنه و Thread Safety صحبت شد.'; stage = 'بررسی Scoped، Singleton و Transient با مثال BackgroundService و IServiceScopeFactory.' }
    @{ company = 'نمونهٔ آموزشی: تیم وب'; level = 'Mid'; question = 'Middleware Pipeline در ASP.NET Core چگونه کار می‌کند و ترتیب آن چه اهمیتی دارد؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۳ PDF: ترتیب Routing، CORS، Authentication و Authorization بررسی شد و اثر جابه‌جایی آن‌ها بر پاسخ‌های ۴۰۱ و ۴۰۳ توضیح داده شد.'; stage = 'مرور مسیر درخواست و پاسخ و نقش هر Middleware در Pipeline.' }
    @{ company = 'نمونهٔ آموزشی: تیم داده'; level = 'Mid'; question = 'تفاوت IEnumerable، IQueryable و List در دسترسی به داده چیست؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۴ PDF: یک Query در EF Core بررسی شد تا معلوم شود فیلتر قبل یا بعد از ToList در SQL اجرا می‌شود یا در حافظه.'; stage = 'مقایسهٔ deferred execution در IQueryable با بارگذاری کامل داده در List.' }
    @{ company = 'نمونهٔ آموزشی: تیم داده'; level = 'Senior'; question = 'Change Tracking و AsNoTracking در EF Core چگونه کار می‌کنند و چه زمانی باید از هرکدام استفاده کرد؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۵ PDF: برای گزارش فقط‌خواندنی AsNoTracking انتخاب شد و دربارهٔ هزینهٔ Tracking و به‌روزرسانی Entity جداشده بحث شد.'; stage = 'بررسی ChangeTracker و رفتار SaveChanges در Queryهای فقط‌خواندنی و ویرایشی.' }
    @{ company = 'نمونهٔ آموزشی: تیم داده'; level = 'Senior'; question = 'مشکل N+1 Query چیست، چگونه تشخیص داده می‌شود و چگونه رفع می‌گردد؟'; description = 'نمونهٔ آموزشی بر پایهٔ سؤال ۶ PDF: تعداد Queryهای بارگذاری فهرست و زیرمجموعه‌های آن اندازه‌گیری شد و Projection، Include و AsSplitQuery مقایسه شدند.'; stage = 'تحلیل لاگ SQL و انتخاب راهکار مناسب برای جلوگیری از Queryهای تکراری.' }
)

$existing = Invoke-RestMethod "$base/experiences?page=1&pageSize=100"
if (@($existing.items | Where-Object { $_.companyName -like 'نمونهٔ آموزشی:*' }).Count -gt 0) {
    throw 'Educational PDF samples already exist; refusing to create duplicates.'
}

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = "DemoPdf!$suffix-4826"
$candidate = Invoke-RestMethod "$base/auth/register" -Method Post -ContentType 'application/json' -Body (@{ userName = "demo_pdf_$suffix"; email = "demo_pdf_$suffix@interviewhub.local"; password = $password; role = 'Candidate' } | ConvertTo-Json)
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
if (-not $adminLine) { throw 'Admin password is unavailable.' }
$admin = Invoke-RestMethod "$base/auth/login" -Method Post -ContentType 'application/json' -Body (@{ userNameOrEmail = 'admin'; password = $adminLine.Substring('Admin:Password = '.Length) } | ConvertTo-Json)
$candidateHeaders = @{ Authorization = "Bearer $($candidate.accessToken)" }
$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }

for ($i = 0; $i -lt $samples.Count; $i++) {
    $sample = $samples[$i]
    $date = (Get-Date).Date.AddDays(-$i).ToString('yyyy-MM-dd')
    $body = @{
        companyName = $sample.company; isAuthorAnonymous = $true; level = $sample.level; outcome = 'Unknown'
        description = $sample.description
        stages = @(@{ type = 'Technical'; date = $date; description = $sample.stage; questions = @(@{ text = $sample.question; type = 'Conceptual' }) })
    } | ConvertTo-Json -Depth 10
    $created = Invoke-RestMethod "$base/candidate-experiences" -Method Post -Headers $candidateHeaders -ContentType 'application/json' -Body $body
    $review = @{ reason = 'Reviewed educational sample derived from the supplied PDF; not a real interview.' } | ConvertTo-Json
    Invoke-RestMethod "$base/admin/experiences/candidate/$($created.id)/publish" -Method Post -Headers $adminHeaders -ContentType 'application/json' -Body $review | Out-Null
    Write-Output "Published educational sample $($i + 1): $($created.id)"
}
