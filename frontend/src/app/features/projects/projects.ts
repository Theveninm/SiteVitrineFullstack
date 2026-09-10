import { Component } from '@angular/core';
import { PROJECTS } from './project-data';

@Component({
  selector: 'app-projects',
  imports: [],
  templateUrl: './projects.html',
  styleUrl: './projects.scss',
})
export class Projects {
  protected readonly projects = PROJECTS;
}
