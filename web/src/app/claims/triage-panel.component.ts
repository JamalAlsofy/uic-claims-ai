import { Component, effect, inject, input, signal } from '@angular/core';
import { DecimalPipe, NgClass } from '@angular/common';
import { AiClientService, TriageSuggestion } from './ai-client.service';

@Component({
  selector: 'app-triage-panel',
  standalone: true,
  imports: [DecimalPipe, NgClass],
  template: `
    <section class="panel" dir="rtl">
      <div class="panel-head">
        <h2>اقتراح النظام</h2>
        @if (suggestion(); as s) {
          <span class="risk" [ngClass]="riskClass(s.riskScore)">{{ riskLabel(s.riskScore) }}</span>
        }
      </div>

      @if (client.busy()) {
        <p class="muted">جارٍ التحليل…</p>
      } @else if (suggestion(); as s) {
        <dl class="facts">
          <dt>التصنيف</dt><dd>{{ s.category }}</dd>
          <dt>درجة الثقة</dt><dd>{{ s.confidence | number: '1.0-0' }}%</dd>
          <dt>مؤشر الخطر</dt><dd>{{ s.riskScore }}</dd>
        </dl>

        @if (s.flags.length > 0) {
          <ul class="flags" aria-label="علامات">
            @for (flag of s.flags; track flag) { <li>{{ flag }}</li> }
          </ul>
        }

        <p class="summary">{{ s.summaryAr }}</p>

        @if (s.requiresHumanReview) {
          <p class="review-required" role="status">يتطلب قرارك قبل أي إجراء.</p>
        }

        <div class="actions">
          <button type="button" class="primary" (click)="accept(s)">اعتماد الاقتراح</button>
          <button type="button" (click)="startOverride(s)">تعديل</button>
          <button type="button" class="quiet" (click)="suggestion.set(null)">تجاهل</button>
        </div>

        @if (overriding()) {
          <form class="override" (submit)="submitOverride($event)">
            <label>
              التصنيف الصحيح
              <input [value]="category()" (input)="category.set($any($event.target).value)" required />
            </label>
            <label>
              سبب التعديل
              <textarea rows="2" [value]="reason()" (input)="reason.set($any($event.target).value)"></textarea>
            </label>
            <button type="submit">حفظ التعديل</button>
          </form>
        }
      }

      @if (client.problem(); as problem) {
        <p class="error">{{ problem }}</p>
        <button type="button" (click)="load()">إعادة المحاولة</button>
      }
    </section>
  `
})
export class TriagePanelComponent {
  readonly claimId = input.required<number>();
  readonly client = inject(AiClientService);

  readonly suggestion = signal<TriageSuggestion | null>(null);
  readonly overriding = signal(false);
  readonly category = signal('');
  readonly reason = signal('');

  constructor() {
    // Reload whenever the officer moves to a different claim.
    effect(() => {
      this.claimId();
      void this.load();
    });
  }

  async load(): Promise<void> {
    this.suggestion.set(await this.client.triage(this.claimId()));
  }

  async accept(s: TriageSuggestion): Promise<void> {
    if (await this.client.sendFeedback(this.claimId(), 'accepted')) {
      this.suggestion.set(null);
    }
  }

  startOverride(s: TriageSuggestion): void {
    this.category.set(s.category);
    this.overriding.set(true);
  }

  async submitOverride(event: Event): Promise<void> {
    event.preventDefault();
    const ok = await this.client.sendFeedback(
      this.claimId(), 'overridden', this.category(), this.reason());
    if (ok) {
      this.overriding.set(false);
      this.suggestion.set(null);
    }
  }

  riskClass(score: number): string {
    return score >= 60 ? 'risk-high' : score >= 35 ? 'risk-medium' : 'risk-low';
  }

  riskLabel(score: number): string {
    return score >= 60 ? 'خطر مرتفع' : score >= 35 ? 'خطر متوسط' : 'خطر منخفض';
  }
}
