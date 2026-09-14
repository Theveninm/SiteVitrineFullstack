import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { form, required } from '@angular/forms/signals';

import { FieldError } from './field-error';

describe('FieldError', () => {
  let fixture: ComponentFixture<FieldError>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [FieldError] }).compileComponents();
    fixture = TestBed.createComponent(FieldError);
  });

  it('n’affiche l’erreur qu’une fois le champ touché', () => {
    // form() doit être appelé dans un contexte d'injection, comme dans un composant.
    const fields = TestBed.runInInjectionContext(() =>
      form(signal({ name: '' }), (path) => required(path.name, { message: 'Obligatoire.' })),
    );
    fixture.componentRef.setInput('field', fields.name);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Obligatoire.');

    fields.name().markAsTouched();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Obligatoire.');
  });
});
