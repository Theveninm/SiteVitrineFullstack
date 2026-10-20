import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { Ressources } from './ressources';
import { Resource } from './resource.model';

const FAKE_RESOURCE: Resource = {
  slug: 'linqpad',
  title: 'LINQPad',
  avis: 'Un avis.',
  positifs: ['rapide'],
  negatifs: ['payant'],
  note: 4,
};

describe('Ressources', () => {
  let component: Ressources;
  let fixture: ComponentFixture<Ressources>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Ressources],
      // Le composant consomme désormais l'API : provideHttpClientTesting() remplace le backend
      // par un contrôleur de test, aucune requête réseau n'est réellement émise.
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Ressources);
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
  async function respondWith(resources: Resource[]): Promise<void> {
    httpMock.expectOne('/api/resources').flush(resources);
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

  it('affiche les ressources renvoyées par l’API', async () => {
    await respondWith([FAKE_RESOURCE]);

    expect(fixture.nativeElement.textContent).toContain('LINQPad');
  });

  it('affiche un message et un bouton de reprise si l’API échoue', async () => {
    httpMock
      .expectOne('/api/resources')
      .flush('boom', { status: 500, statusText: 'Internal Server Error' });
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.content-status--error')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.content-status__retry')).toBeTruthy();
  });
});
