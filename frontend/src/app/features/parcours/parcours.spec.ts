import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Parcours } from './parcours';

describe('Parcours', () => {
  let component: Parcours;
  let fixture: ComponentFixture<Parcours>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Parcours],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Parcours);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
