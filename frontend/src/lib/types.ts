export type Role = 'Candidate' | 'Interviewer' | 'Admin'
export type QuestionType = 'Algorithm' | 'LiveCode' | 'Conceptual' | 'SystemDesign'
export type Level = 'Unknown' | 'Intern' | 'Junior' | 'Mid' | 'Senior' | 'Lead'
export type Outcome = 'Unknown' | 'Passed' | 'Rejected'
export type StageType = 'HR' | 'Technical' | 'LiveCoding' | 'SystemDesign' | 'Other'
export type ModerationStatus = 'Pending' | 'Published' | 'Hidden' | 'Rejected' | 'Deleted'

export interface Page<T> { page: number; pageSize: number; totalCount: number; items: T[] }
export interface TokenResponse { accessToken: string; tokenType: string; expiresAt: string; roles: Role[] }
export interface Account { id: string; userName: string; email: string; roles: Role[] }
export interface Company { id: string; name: string }
export interface Tag { id: string; name: string }
export interface Question { id: string; text: string; type: QuestionType; isReferenceQuestion: boolean; askedCount: number; tags: Tag[] }
export interface ExperienceItem {
  id: string; kind: 'candidate' | 'interviewer'; interviewDate: string; level: Level;
  authorName: string | null; companyName: string | null; interviewRating: number | null;
  interviewerConductRating: number | null; jobAdAlignmentRating: number | null
}
export interface StageQuestion { questionId: string; text: string; type: QuestionType; perceivedDifficulty: number | null }
export interface Stage { id: string; order: number; type: StageType; date: string; description: string; questions: StageQuestion[] }
export interface CandidateDetail {
  id: string; authorName: string | null; companyName: string | null; interviewDate: string; level: Level;
  outcome: Outcome; description: string | null; jobAdText: string | null; jobAdUrl: string | null;
  interviewerConductRating: number | null; jobAdAlignmentRating: number | null; ratingReason: string | null; stages: Stage[]
}
export interface PrivateCandidateDetail extends CandidateDetail { authorId: string; isAuthorAnonymous: boolean; status: ModerationStatus }
export interface InterviewerDetail {
  id: string; interviewerName: string | null; companyName: string | null; interviewDate: string; level: Level;
  description: string; jobAdText: string | null; jobAdUrl: string | null; interviewRating: number;
  questions: { text: string; type: QuestionType }[]
}
export interface PrivateInterviewerDetail extends InterviewerDetail { interviewerName: string; companyName: string; showInterviewerName: boolean; showCompanyName: boolean; status: ModerationStatus }
export interface Priority { skill: string; importance: number; note: string | null }
export interface PrivateProfile {
  userId: string; publicId: string; displayName: string; companyName: string | null; bio: string | null;
  adviceText: string | null; showName: boolean; showCompany: boolean; showBio: boolean;
  status: ModerationStatus; priorities: Priority[]
}
export interface CompanyReport {
  companyId: string; available: boolean; experienceCount: number | null; passRate: number | null;
  knownOutcomeCount: number | null; conductRating: number | null; conductRatingCount: number | null;
  adAlignmentRating: number | null; adAlignmentRatingCount: number | null;
  topQuestions: { questionId: string; text: string; type: QuestionType; experienceCount: number }[];
  difficultyDistribution: Record<string, number>
}
export interface MarketReport {
  refreshedAt: string | null; topQuestions: { questionId: string; text: string; type: QuestionType; experienceCount: number }[];
  typeDistribution: { type: QuestionType; questionMentions: number }[];
  topTags: { tagId: string; name: string; questionMentions: number }[];
  trendMetric: string; trend: { year: number; month: number; count: number }[]
}
export interface CandidateInput {
  companyName?: string; isAuthorAnonymous: boolean; level: Level; outcome: Outcome;
  description?: string; jobAdText?: string; jobAdUrl?: string;
  interviewerConductRating?: number; jobAdAlignmentRating?: number; ratingReason?: string;
  stages: { type: StageType; date: string; description: string;
    questions: { questionId?: string; text?: string; type: QuestionType; perceivedDifficulty?: number }[] }[]
}
