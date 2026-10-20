import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { Projects } from './projects';
import { Project } from './project.model';

const FAKE_PROJECT: Project = {
  slug: 'docfileeditor',
  title: 'DocFileEditor',
  description: 'Une description.',
  techs: ['C#', 'WPF'],
  githubUrl: 'https://github.com/Theveninm/DocFileEditor',
};

describe('Projects', () => {
  let component: Projects;
  let fixture: ComponentFixture<Projects>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Projects],
      // Le composant consomme désormais l'API : provideHttpClientTesting() remplace le backend
      // par un contrôleur de test, aucune requête réseau n'est réellement émise.
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Projects);
    component = fixture.componentInstance;

    // detectChanges() et non whenStable() : c'est le premier cycle de détection qui exécute
    // l'effet interne du httpResource, donc qui émet la requête. whenStable() attendrait au
    // contraire que cette requête soit terminée — or c'est au test de la servir, juste après.
    fixture.detectChanges();
  });

  /**
   * Le httpResource part dès l'instanciation du service : chaque test doit donc répondre à
   * cette requête, sinon httpMock.verify() la signale comme restée en attente.
   */
  async function respondWith(projects: Project[]): Promise<void> {
    httpMock.expectOne('/api/projects').flush(projects);
    await fixture.whenStable();
  }

  afterEach(() => {
    // Échoue si une requête inattendue a été émise, ou si une requête attendue ne l'a pas été.
    httpMock.verify();
  });

  it('should create', async () => {
    await respondWith([]);

    expect(component).toBeTruthy();
  });

  it('affiche les projets renvoyés par l’API, avec leurs technologies', async () => {
    await respondWith([FAKE_PROJECT]);

    expect(fixture.nativeElement.textContent).toContain('DocFileEditor');
    expect(fixture.nativeElement.textContent).toContain('WPF');
  });

  it('n’affiche le lien de démonstration que si le projet en a une', async () => {
    await respondWith([FAKE_PROJECT]);

    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.project__links a'),
    );
    expect(links.map((link) => link.textContent?.trim())).toEqual(['Voir sur GitHub']);
  });

  it('affiche un message et un bouton de reprise si l’API échoue', async () => {
    httpMock
      .expectOne('/api/projects')
      .flush('boom', { status: 500, statusText: 'Internal Server Error' });
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.content-status--error')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.content-status__retry')).toBeTruthy();
  });
});
