import { Component } from '@angular/core';
import { CONTACT_EMAIL } from '../../shared/contact-info';

@Component({
  selector: 'app-contact',
  imports: [],
  templateUrl: './contact.html',
  styleUrl: './contact.scss',
})
export class Contact {
  protected readonly email = CONTACT_EMAIL;
}
