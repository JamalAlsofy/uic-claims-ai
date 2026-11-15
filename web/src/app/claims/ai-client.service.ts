import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TriageSuggestion {
  category: string;
  confidence: number;
  riskScore: number;
  flags: string[];
  summaryAr: string;
  requiresHumanReview: boolean;
}

export type FeedbackOutcome = 'accepted' | 'overridden';

/**
 * Talks to our API only. The provider key lives on the server; nothing in this file
 * or in the bundle references it.
 */
@Injectable({ providedIn: 'root' })
export class AiClientService {
  private readonly http = inject(HttpClient);

  readonly busy = signal(false);
  readonly problem = signal<string | null>(null);

  async triage(claimId: number): Promise<TriageSuggestion | null> {
    this.busy.set(true);
    this.problem.set(null);

    try {
      return await firstValueFrom(
        this.http.post<TriageSuggestion>(`/api/ai/claims/${claimId}/triage`, {}));
    } catch (error) {
      this.problem.set(describe(error));
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  async sendFeedback(
    claimId: number,
    outcome: FeedbackOutcome,
    finalCategory: string | null = null,
    comment: string | null = null): Promise<boolean> {
    try {
      await firstValueFrom(this.http.post<void>(
        `/api/ai/claims/${claimId}/feedback`,
        { outcome, finalCategory, comment }));
      return true;
    } catch (error) {
      this.problem.set(describe(error));
      return false;
    }
  }
}

function describe(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    switch (error.status) {
      case 0: return 'تعذّر الوصول إلى الخادم.';
      case 404: return 'المطالبة غير موجودة.';
      case 429: return 'تجاوزت الحد المسموح لهذه الدقيقة، انتظر قليلاً.';
      case 503: return 'خدمة التحليل غير متاحة، يمكنك المتابعة يدوياً.';
      default: return `خطأ غير متوقع (${error.status}).`;
    }
  }
  return 'خطأ غير متوقع.';
}
