import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';

import { Login } from './login';

describe('Login', () => {
  /** Crée la page comme si l'adresse portait ces paramètres (?erreur=…). */
  async function createWith(queryParams: Record<string, string>): Promise<ComponentFixture<Login>> {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
    return fixture;
  }

  it('propose la connexion avec Google', async () => {
    const fixture = await createWith({});

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a');
    expect(link.getAttribute('href')).toBe('/api/auth/login');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('explique un compte refusé', async () => {
    const fixture = await createWith({ erreur: 'refus' });

    expect(fixture.nativeElement.textContent).toContain('n’est pas autorisé');
  });
});
