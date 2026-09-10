import { Component } from '@angular/core';
import { CONTACT_EMAIL, GITHUB_URL } from '../../contact-info';

@Component({
  selector: 'app-footer',
  imports: [],
  templateUrl: './footer.html',
  styleUrl: './footer.scss',
})
export class Footer {
  protected readonly email = CONTACT_EMAIL;
  protected readonly githubUrl = GITHUB_URL;
  protected readonly year = new Date().getFullYear();
}
