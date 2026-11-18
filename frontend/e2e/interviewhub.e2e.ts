import { expect, test, type Page } from '@playwright/test'

const appUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:5173'
const adminUsername = process.env.E2E_ADMIN_USERNAME || 'e2e-admin'
const adminPassword = process.env.E2E_ADMIN_PASSWORD || 'E2EAdmin!4826'
const disposablePassword = 'InterviewHubE2E!4826'

type TestAccount = { userName: string; email: string; password: string }

function createAccount(role: 'candidate' | 'interviewer'): TestAccount {
  const suffix = `${Date.now()}_${Math.random().toString(36).slice(2, 7)}`
  return {
    userName: `e2e_${role}_${suffix}`,
    email: `e2e_${role}_${suffix}@example.test`,
    password: disposablePassword,
  }
}

async function registerThroughUi(
  page: Page,
  account: TestAccount,
  role: 'Candidate' | 'Interviewer',
) {
  await page.goto('/register')
  await page.getByLabel('نام کاربری').fill(account.userName)
  await page.getByLabel('ایمیل').fill(account.email)
  await page.getByLabel('رمز عبور').fill(account.password)
  await page.getByLabel('نوع مشارکت').selectOption(role)
  await page.getByRole('button', { name: 'ثبت‌نام' }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(
    page.getByRole('heading', { name: new RegExp(account.userName) }),
  ).toBeVisible()
}

async function loginThroughUi(
  page: Page,
  account: Pick<TestAccount, 'userName' | 'password'>,
) {
  await page.goto('/login')
  await page.getByLabel('نام کاربری یا ایمیل').fill(account.userName)
  await page.getByLabel('رمز عبور').fill(account.password)
  await page.getByRole('button', { name: 'ورود به حساب' }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
}

async function loginAsAdmin(page: Page) {
  await loginThroughUi(page, {
    userName: adminUsername,
    password: adminPassword,
  })
  await page.getByRole('link', { name: 'ورود به پنل بررسی' }).click()
  await expect(page.getByRole('heading', { name: 'صف بررسی' })).toBeVisible()
}

async function logoutThroughUi(page: Page) {
  await page.getByRole('button', { name: 'خروج' }).click()
  await expect(page).toHaveURL(/\/login$/)
}

async function openAdminPendingQueue(page: Page) {
  await expect(page).toHaveURL(/\/admin$/)
  const experienceCard = page.locator('.admin-columns .card').first()
  await expect(
    experienceCard.getByRole('heading', { name: 'تجربه‌های در انتظار' }),
  ).toBeVisible()
  return experienceCard
}

async function publicApi(page: Page, path: string) {
  return page.request.get(new URL(path, appUrl).toString())
}

test('Candidate submits, receives a reasoned rejection, edits, gets approval, then deletes the experience', async ({
  page,
}) => {
  const candidate = createAccount('candidate')
  const company = `E2E Candidate ${Date.now()}`
  const question = `Explain dependency injection ${Date.now()}`

  // This journey uses registration and the real multi-step Candidate form.
  await registerThroughUi(page, candidate, 'Candidate')
  await page.getByRole('link', { name: 'ثبت تجربهٔ مصاحبه‌شونده' }).click()
  await page.getByLabel('نام شرکت (اختیاری)').fill(company)
  await page.getByLabel('سطح مصاحبه').selectOption('Senior')
  await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page
    .getByLabel('چه اتفاقی افتاد؟')
    .fill('Discussed API boundaries and production incidents.')
  await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page.getByLabel('سؤال 1').fill(question)
  await page.getByLabel('نوع سؤال').selectOption('Conceptual')
  await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page.getByLabel('نتیجهٔ مصاحبه').selectOption('Passed')
  await page.getByLabel('رفتار مصاحبه‌کننده').selectOption('4')
  await page.getByLabel('تناسب سؤال‌ها با آگهی').selectOption('5')
  await page
    .getByLabel('دلیل امتیازها (اختیاری)')
    .fill('The questions matched the backend job ad.')
  await page
    .getByLabel('توصیه یا توضیح تکمیلی (اختیاری)')
    .fill('A realistic interview experience.')
  await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page.getByRole('button', { name: 'ثبت نهایی تجربه' }).click()
  await expect(page).toHaveURL(
    /\/dashboard\/submission\/candidate\/[0-9a-f-]+$/,
  )
  await expect(page.locator('.receipt-card')).toContainText('در انتظار بررسی')
  const candidateId = page.url().match(/candidate\/([0-9a-f-]+)$/)?.[1]
  expect(candidateId).toBeTruthy()

  await logoutThroughUi(page)
  await loginAsAdmin(page)
  let experienceCard = await openAdminPendingQueue(page)
  const candidateRow = experienceCard
    .locator('.admin-row')
    .filter({ hasText: 'مصاحبه‌شونده' })
  await expect(candidateRow).toBeVisible()
  await page
    .getByLabel('دلیل تصمیم (برای تأیید یا رد اجباری است)')
    .fill('Please clarify the interview stage.')
  await candidateRow.getByRole('button', { name: 'رد' }).click()
  await expect(candidateRow).toHaveCount(0)

  // Editing a rejected record returns it to moderation instead of publishing silently.
  await logoutThroughUi(page)
  await loginThroughUi(page, candidate)
  let ownedExperience = page
    .locator('.owner-record')
    .filter({ hasText: company })
  await expect(ownedExperience).toContainText('ردشده')
  await ownedExperience.getByRole('link', { name: 'ویرایش' }).click()
  for (let step = 0; step < 3; step++)
    await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page
    .getByLabel('توصیه یا توضیح تکمیلی (اختیاری)')
    .fill('Updated with a clearer stage description.')
  await page.getByRole('button', { name: /مرحلهٔ بعد/ }).click()
  await page.getByRole('button', { name: 'ذخیرهٔ ویرایش برای بررسی' }).click()
  await expect(page).toHaveURL(
    new RegExp(`/dashboard/submission/candidate/${candidateId}$`),
  )
  await expect(page.locator('.receipt-card')).toContainText('در انتظار بررسی')

  await logoutThroughUi(page)
  await loginAsAdmin(page)
  experienceCard = await openAdminPendingQueue(page)
  const editedCandidateRow = experienceCard
    .locator('.admin-row')
    .filter({ hasText: 'مصاحبه‌شونده' })
  await page
    .getByLabel('دلیل تصمیم (برای تأیید یا رد اجباری است)')
    .fill('Revised submission reviewed and approved.')
  await editedCandidateRow.getByRole('button', { name: 'تأیید' }).click()
  await expect(editedCandidateRow).toHaveCount(0)
  const published = await publicApi(
    page,
    `/api/candidate-experiences/${candidateId}`,
  )
  expect(published.status()).toBe(200)
  expect(await published.json()).toMatchObject({
    companyName: company,
    outcome: 'Passed',
  })

  // The owner can remove published content, which also removes its public endpoint.
  await logoutThroughUi(page)
  await loginThroughUi(page, candidate)
  ownedExperience = page.locator('.owner-record').filter({ hasText: company })
  await expect(ownedExperience).toContainText('منتشرشده')
  const deleteDialog = page.waitForEvent('dialog')
  await ownedExperience.getByRole('button', { name: 'حذف' }).click()
  await (await deleteDialog).accept()
  await expect(ownedExperience).toContainText('حذف‌شده')
  expect(
    (
      await publicApi(page, `/api/candidate-experiences/${candidateId}`)
    ).status(),
  ).toBe(404)

  await page.goto('/reports')
  await expect(
    page.getByRole('heading', { name: 'گزارش بازار مصاحبه' }),
  ).toBeVisible()
})

test('Interviewer publishes anonymous feedback and advice, manages both, and keeps the rating public', async ({
  page,
}) => {
  const interviewer = createAccount('interviewer')
  const company = `E2E Interviewer ${Date.now()}`
  const adviceSkill = `Architecture ${Date.now()}`
  const question = `Design a cache ${Date.now()}`

  // Profile advice and feedback are submitted by a real Interviewer account.
  await registerThroughUi(page, interviewer, 'Interviewer')
  await page.getByRole('link', { name: 'ثبت توصیه' }).click()
  await page.getByLabel('نام نمایشی').fill('E2E Anonymous Interviewer')
  await page.getByLabel('شرکت (اختیاری)').fill(company)
  await page
    .getByLabel('دربارهٔ من (اختیاری)')
    .fill('Backend engineering lead.')
  await page
    .getByLabel('توصیه‌ها')
    .fill('Prioritize architecture and clear communication.')
  await page.getByLabel('مهارت 1').fill(adviceSkill)
  await page.getByLabel('میزان اهمیت').selectOption('5')
  const profileSaved = page.waitForResponse(
    (response) =>
      response.url().includes('/api/interviewer-profiles/me') &&
      response.request().method() === 'PUT',
  )
  await page.getByRole('button', { name: 'ذخیره برای بررسی مدیر' }).click()
  const privateProfile = (await (await profileSaved).json()) as {
    publicId: string
    userId: string
  }

  await page.getByRole('link', { name: interviewer.userName }).click()
  await page.getByRole('link', { name: 'ثبت دیدگاه مصاحبه‌کننده' }).click()
  await page.getByLabel('نام مصاحبه‌کننده').fill('Hidden Interviewer')
  await page.getByLabel('نام شرکت').fill(company)
  await page
    .getByLabel('تجربه و ارزیابی مصاحبه')
    .fill('The candidate explained system trade-offs clearly.')
  await page
    .getByLabel('متن آگهی (اختیاری)')
    .fill('Backend role focused on distributed systems.')
  await page.getByLabel('امتیاز مصاحبه (همیشه عمومی)').selectOption('4')
  await page.getByLabel('سؤال 1').fill(question)
  await page.getByLabel('نوع سؤال').selectOption('SystemDesign')
  await page.getByRole('button', { name: 'ثبت دیدگاه برای بررسی' }).click()
  await expect(page).toHaveURL(
    /\/dashboard\/submission\/interviewer\/[0-9a-f-]+$/,
  )
  const interviewerId = page.url().match(/interviewer\/([0-9a-f-]+)$/)?.[1]
  expect(interviewerId).toBeTruthy()

  await logoutThroughUi(page)
  await loginAsAdmin(page)
  const experienceCard = await openAdminPendingQueue(page)
  const interviewerRow = experienceCard
    .locator('.admin-row')
    .filter({ hasText: 'مصاحبه‌کننده' })
  await page
    .getByLabel('دلیل تصمیم (برای تأیید یا رد اجباری است)')
    .fill('The content and privacy settings were reviewed.')
  await interviewerRow.getByRole('button', { name: 'تأیید' }).click()
  await expect(interviewerRow).toHaveCount(0)
  const profileCard = page.locator('.admin-columns .card').nth(1)
  const profileRow = profileCard
    .locator('.admin-row')
    .filter({ hasText: 'دیدگاه مصاحبه‌کننده' })
  await profileRow.getByRole('button', { name: 'تأیید' }).click()
  await expect(profileRow).toHaveCount(0)

  const publicFeedback = await publicApi(
    page,
    `/api/interviewer-experiences/${interviewerId}`,
  )
  expect(publicFeedback.status()).toBe(200)
  expect(await publicFeedback.json()).toMatchObject({
    interviewerName: null,
    companyName: null,
    interviewRating: 4,
  })
  const publicAdvice = await publicApi(
    page,
    `/api/interviewer-profiles/${privateProfile.publicId}`,
  )
  expect(publicAdvice.status()).toBe(200)
  expect(await publicAdvice.json()).toMatchObject({
    displayName: null,
    adviceText: 'Prioritize architecture and clear communication.',
  })

  // The Interviewer can edit published feedback, then delete both owned records.
  await logoutThroughUi(page)
  await loginThroughUi(page, interviewer)
  let ownedFeedback = page.locator('.owner-record').filter({ hasText: company })
  await expect(ownedFeedback).toContainText('منتشرشده')
  await ownedFeedback.getByRole('link', { name: 'ویرایش' }).click()
  await page
    .getByLabel('تجربه و ارزیابی مصاحبه')
    .fill('Edited feedback with clearer evidence.')
  await page.getByRole('button', { name: 'ذخیرهٔ ویرایش برای بررسی' }).click()
  await expect(page.locator('.receipt-card')).toContainText('در انتظار بررسی')
  expect(
    (
      await publicApi(page, `/api/interviewer-experiences/${interviewerId}`)
    ).status(),
  ).toBe(404)

  await logoutThroughUi(page)
  await loginAsAdmin(page)
  const revisedRow = (await openAdminPendingQueue(page))
    .locator('.admin-row')
    .filter({ hasText: 'مصاحبه‌کننده' })
  await page
    .getByLabel('دلیل تصمیم (برای تأیید یا رد اجباری است)')
    .fill('The revised feedback is approved.')
  await revisedRow.getByRole('button', { name: 'تأیید' }).click()
  await expect(revisedRow).toHaveCount(0)

  await logoutThroughUi(page)
  await loginThroughUi(page, interviewer)
  ownedFeedback = page.locator('.owner-record').filter({ hasText: company })
  const feedbackDeleteDialog = page.waitForEvent('dialog')
  await ownedFeedback.getByRole('button', { name: 'حذف' }).click()
  await (await feedbackDeleteDialog).accept()
  await expect(ownedFeedback).toContainText('حذف‌شده')
  const adviceDeleteDialog = page.waitForEvent('dialog')
  await page.getByRole('button', { name: 'حذف توصیه‌ها' }).click()
  await (await adviceDeleteDialog).accept()
  await expect(page.getByRole('link', { name: 'ثبت توصیه' })).toBeVisible()
  expect(
    (
      await publicApi(page, `/api/interviewer-experiences/${interviewerId}`)
    ).status(),
  ).toBe(404)
  expect(
    (
      await publicApi(
        page,
        `/api/interviewer-profiles/${privateProfile.publicId}`,
      )
    ).status(),
  ).toBe(404)

  const priorities = await publicApi(
    page,
    '/api/reports/interviewers/priorities',
  )
  expect(priorities.status()).toBe(200)
  expect(JSON.stringify(await priorities.json())).not.toContain(adviceSkill)
})
