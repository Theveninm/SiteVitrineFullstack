import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageDrop } from './image-drop';

describe('ImageDrop', () => {
  let fixture: ComponentFixture<ImageDrop>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageDrop],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ImageDrop);
    fixture.detectChanges();
    // Le service d'écriture injecte la liste partagée des ressources, dont le httpResource part dès sa
    // création. Il faut la servir avant whenStable(), qui sinon l'attendrait indéfiniment.
    httpMock.expectOne('/api/resources').flush([]);
    await fixture.whenStable();
  });

  afterEach(() => {
    httpMock.verify();
  });

  /** jsdom ne sait pas construire de DragEvent avec des fichiers : on greffe un dataTransfer minimal. */
  function dropFile(file: File): void {
    const event = Object.assign(new Event('drop', { cancelable: true }), {
      dataTransfer: { files: [file] },
    });
    fixture.nativeElement.querySelector('.image-drop').dispatchEvent(event);
  }

  it('envoie l’image déposée et prend le nom renvoyé par l’API', async () => {
    dropFile(new File(['png'], 'logo.png', { type: 'image/png' }));

    const request = httpMock.expectOne('/api/images');
    expect(request.request.body).toBeInstanceOf(FormData);
    request.flush({ fileName: '3f9ac1.png' });
    await fixture.whenStable();

    expect(fixture.componentInstance.value()).toBe('3f9ac1.png');
    expect(fixture.nativeElement.querySelector('img').getAttribute('src')).toBe('/api/images/3f9ac1.png');
  });

  it('refuse une image trop lourde sans l’envoyer', async () => {
    dropFile(new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'lourde.png'));
    await fixture.whenStable();

    httpMock.expectNone('/api/images');
    expect(fixture.nativeElement.textContent).toContain('5 Mo');
  });
});
