import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Project } from '../../projects/project.model';
import { ProjectList } from './project-list';

const FAKE_PROJECTS: Project[] = [
  { slug: 'docfileeditor', title: 'DocFileEditor', description: 'Une description.', techs: [] },
  { slug: 'site-personnel', title: 'Site personnel', description: 'Une description.', techs: [] },
];

describe('ProjectList', () => {
  let fixture: ComponentFixture<ProjectList>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProjectList);
    // Premier cycle de détection : c'est lui qui émet la requête du httpResource (cf. projects.spec).
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  /** Sert la liste. Le reload() du constructeur a pu annuler une première requête : on l'ignore. */
  async function respondWith(projects: Project[]): Promise<void> {
    for (const request of httpMock.match('/api/projects').filter((r) => !r.cancelled)) {
      request.flush(projects);
    }
    await fixture.whenStable();
  }

  it('affiche les projets de l’API', async () => {
    await respondWith(FAKE_PROJECTS);

    expect(fixture.nativeElement.querySelectorAll('.admin-list__item').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Site personnel');
  });

  it('supprime un projet après confirmation', async () => {
    await respondWith(FAKE_PROJECTS);
    globalThis.confirm = () => true;

    fixture.nativeElement.querySelector('.admin-button--danger').click();

    const request = httpMock.expectOne('/api/projects/docfileeditor');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);

    // Après une écriture, la liste est rechargée depuis l'API.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await respondWith([FAKE_PROJECTS[1]]);
  });
});
