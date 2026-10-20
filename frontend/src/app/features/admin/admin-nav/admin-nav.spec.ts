import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AdminNav } from './admin-nav';

describe('AdminNav', () => {
  let fixture: ComponentFixture<AdminNav>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminNav],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // La déconnexion navigue vers l'écran de connexion : sans cette route, le routeur de test
        // rejette la navigation (NG04002) et Vitest signale une promesse non traitée.
        provideRouter([{ path: 'admin/connexion', children: [] }]),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AdminNav);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('propose un onglet par section éditable', () => {
    const tabs: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.admin-nav__tab'),
    );

    expect(tabs.map((tab) => tab.getAttribute('href'))).toEqual([
      '/admin/ressources',
      '/admin/projets',
    ]);
  });

  it('ferme la session côté serveur à la déconnexion', async () => {
    fixture.nativeElement.querySelector('.admin-button').click();

    const request = httpMock.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null);
    await fixture.whenStable();
  });
});
