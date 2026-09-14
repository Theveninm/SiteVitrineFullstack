import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Resource } from '../../ressources/resource.model';
import { ResourceList } from './resource-list';

const FAKE_RESOURCES: Resource[] = [
  { slug: 'linqpad', title: 'LINQPad', avis: 'Un avis.', positifs: [], negatifs: [], note: 4 },
  { slug: 'vs-code', title: 'VS Code', avis: 'Un avis.', positifs: [], negatifs: [], note: 5 },
];

describe('ResourceList', () => {
  let fixture: ComponentFixture<ResourceList>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ResourceList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ResourceList);
    // Premier cycle de détection : c'est lui qui émet la requête du httpResource (cf. ressources.spec).
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  /** Sert la liste. Le reload() du constructeur a pu annuler une première requête : on l'ignore. */
  async function respondWith(resources: Resource[]): Promise<void> {
    for (const request of httpMock.match('/api/resources').filter((r) => !r.cancelled)) {
      request.flush(resources);
    }
    await fixture.whenStable();
  }

  it('affiche les ressources de l’API', async () => {
    await respondWith(FAKE_RESOURCES);

    expect(fixture.nativeElement.querySelectorAll('.resource-list__item').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('VS Code');
  });

  it('supprime une ressource après confirmation', async () => {
    await respondWith(FAKE_RESOURCES);
    globalThis.confirm = () => true;

    fixture.nativeElement.querySelector('.admin-button--danger').click();

    const request = httpMock.expectOne('/api/resources/linqpad');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);

    // Après une écriture, la liste est rechargée depuis l'API.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await respondWith([FAKE_RESOURCES[1]]);
  });
});
