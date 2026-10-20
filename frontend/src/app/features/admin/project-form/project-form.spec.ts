import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ProjectForm } from './project-form';

describe('ProjectForm', () => {
  let fixture: ComponentFixture<ProjectForm>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectForm],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    // Sans paramètre :slug dans la route, le formulaire est en mode création.
    fixture = TestBed.createComponent(ProjectForm);
    fixture.detectChanges();
    serveProjectList();
    await fixture.whenStable();
  });

  afterEach(() => {
    serveProjectList();
    httpMock.verify();
  });

  /**
   * Le service d'écriture injecte la liste partagée (ProjectService), dont le httpResource part dès
   * sa création puis après chaque écriture. Ces lectures ne sont pas l'objet des tests, mais il faut
   * les servir : whenStable() les attendrait indéfiniment.
   */
  function serveProjectList(): void {
    httpMock
      .match({ method: 'GET', url: '/api/projects' })
      .filter((request) => !request.cancelled)
      .forEach((request) => request.flush([]));
  }

  function type(selector: string, value: string): void {
    const field: HTMLInputElement = fixture.nativeElement.querySelector(selector);
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  it('s’ouvre en création, sans charger de projet', () => {
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Nouveau projet');
    httpMock.expectNone(() => true);
  });

  it('envoie le projet saisi, technologies découpées par ligne', async () => {
    type('#title', 'DocFileEditor');
    type('#slug', 'docfileeditor');
    type('#description', 'Une description.');
    type('#techs', 'C#\n\n  WPF ');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve));

    const request = httpMock.expectOne({ method: 'POST', url: '/api/projects' });
    expect(request.request.body).toMatchObject({
      slug: 'docfileeditor',
      techs: ['C#', 'WPF'],
      githubUrl: null,
      demoUrl: null,
    });
    request.flush(null);
    await new Promise((resolve) => setTimeout(resolve));
  });

  it('refuse un slug mal formé sans contacter l’API', async () => {
    type('#title', 'DocFileEditor');
    type('#slug', 'Doc File');
    type('#description', 'Une description.');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    httpMock.expectNone('/api/projects');
    expect(fixture.nativeElement.textContent).toContain('Minuscules, chiffres et tirets');
  });
});
