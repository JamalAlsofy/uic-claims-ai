import { Component, signal } from '@angular/core';
import { TriagePanelComponent } from './claims/triage-panel.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [TriagePanelComponent],
  template: `
    <header class="topbar">
      <h1>مراجعة المطالبات</h1>
      <label class="claim-picker">
        رقم المطالبة
        <input
          type="number"
          inputmode="numeric"
          [value]="claimId()"
          (input)="onClaimInput($event)"
          aria-label="رقم المطالبة" />
      </label>
    </header>

    @if (claimId()) {
      <app-triage-panel [claimId]="claimId()" />
    } @else {
      <p class="empty">أدخل رقم مطالبة للبدء.</p>
    }
  `
})
export class AppComponent {
  readonly claimId = signal<number | null>(null);

  onClaimInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const parsed = Number.parseInt(raw, 10);
    this.claimId.set(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }
}
