import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ResourceForm } from './resource-form';

describe('ResourceForm', () => {
  let fixture: ComponentFixture<ResourceForm>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ResourceForm],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    // Sans paramètre :slug dans la route, le formulaire est en mode création.
    fixture = TestBed.createComponent(ResourceForm);
    fixture.detectChanges();
    serveResourceList();
    await fixture.whenStable();
  });

  afterEach(() => {
    serveResourceList();
    httpMock.verify();
  });

  /**
   * Le service d'écriture injecte la liste partagée (ResourceService), dont le httpResource part dès
   * sa création puis après chaque écriture. Ces lectures ne sont pas l'objet des tests, mais il faut
   * les servir : whenStable() les attendrait indéfiniment.
   */
  function serveResourceList(): void {
    httpMock
      .match({ method: 'GET', url: '/api/resources' })
      .filter((request) => !request.cancelled)
      .forEach((request) => request.flush([]));
  }

  function type(selector: string, value: string): void {
    const field: HTMLInputElement = fixture.nativeElement.querySelector(selector);
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  it('s’ouvre en création, sans charger de ressource', () => {
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Nouvelle ressource');
    httpMock.expectNone(() => true);
  });

  it('envoie la ressource saisie, points positifs découpés par ligne', async () => {
    type('#title', 'LINQPad');
    type('#slug', 'linqpad');
    type('#avis', 'Un avis.');
    type('#positifs', 'rapide\n\n  pratique ');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));

    const request = httpMock.expectOne({ method: 'POST', url: '/api/resources' });
    expect(request.request.body).toMatchObject({
      slug: 'linqpad',
      imageFile: null,
      positifs: ['rapide', 'pratique'],
      negatifs: [],
    });
    request.flush(null);
    await new Promise((resolve) => setTimeout(resolve));
  });

  it('refuse un slug mal formé sans contacter l’API', async () => {
    type('#title', 'LINQPad');
    type('#slug', 'LINQ Pad');
    type('#avis', 'Un avis.');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    httpMock.expectNone('/api/resources');
    expect(fixture.nativeElement.textContent).toContain('Minuscules, chiffres et tirets');
  });
});
