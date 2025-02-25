import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { IUser, RequestType, UserType } from '../interfaces';
import { SettingsService, SettingsStateService } from '../services';

import { AdvancedSearchDialogComponent } from '../shared/advanced-search-dialog/advanced-search-dialog.component';
import { FilterService } from '../discover/services/filter-service';
import { ILocalUser } from '../auth/IUserLogin';
import { INavBar } from '../interfaces/ICommon';
import { MatDialog } from '@angular/material/dialog';
import { MatSlideToggleChange } from '@angular/material/slide-toggle';
import { Md5 } from 'ts-md5/dist/md5';
import { Observable } from 'rxjs';
import { Router } from '@angular/router';
import { SearchFilter } from './SearchFilter';
import { StorageService } from '../shared/storage/storage-service';
import { map } from 'rxjs/operators';

export enum SearchFilterType {
  Movie = 1,
  TvShow = 2,
  Music = 3,
  People = 4
}

@Component({
  selector: 'app-my-nav',
  templateUrl: './my-nav.component.html',
  styleUrls: ['./my-nav.component.scss'],
})
export class MyNavComponent implements OnInit {

  isHandset$: Observable<boolean> = this.breakpointObserver.observe([Breakpoints.Small, Breakpoints.Handset, Breakpoints.XSmall])
    .pipe(
      map(result => result.matches)
    );

  @Input() public showNav: boolean;
  @Input() public applicationName: string;
  @Input() public applicationLogo: string;
  @Input() public applicationUrl: string;
  @Input() public accessToken: string;
  @Input() public username: string;
  @Input() public isAdmin: string;
  @Input() public email: string;
  @Output() public logoutClick = new EventEmitter();
  public theme: string;
  public issuesEnabled: boolean = false;
  public navItems: INavBar[];
  public searchFilter: SearchFilter;
  public SearchFilterType = SearchFilterType;
  public emailHash: string | Int32Array;
  public welcomeText: string;
  public RequestType = RequestType;

  constructor(private breakpointObserver: BreakpointObserver,
    private settingsService: SettingsService,
    private store: StorageService,
    private filterService: FilterService,
    private dialogService: MatDialog,
    private readonly settingState: SettingsStateService,
    private router: Router) {
  }

  public async ngOnInit() {

    this.searchFilter = {
      movies: true,
      music: false,
      people: false,
      tvShows: true
    }

    if (this.email) {
      const md5 = new Md5();
      this.emailHash = md5.appendStr(this.email).end();
    }

    this.issuesEnabled = await this.settingsService.issueEnabled().toPromise();
    this.settingState.setIssue(this.issuesEnabled);

    const customizationSettings = await this.settingsService.getCustomization().toPromise();
    console.log("issues enabled: " + this.issuesEnabled);
    this.theme = this.store.get("theme");
    if (!this.theme) {
      this.store.save("theme", "dark");
    }
    var filter = this.store.get("searchFilter");
    if (filter) {
      this.searchFilter = Object.assign(new SearchFilter(), JSON.parse(filter));
      this.filterService.changeFilter(this.searchFilter);
    }
    this.navItems = [
      { id: "nav-discover", name: "NavigationBar.Discover", icon: "fas fa-bolt", style:"z-index:-1;", link: "/discover", requiresAdmin: false, enabled: true },
      { id: "nav-requests", name: "NavigationBar.Requests", icon: "fas fa-stream", link: "/requests-list", requiresAdmin: false, enabled: true },
      { id: "nav-issues", name: "NavigationBar.Issues", icon: "fas fa-exclamation-triangle", link: "/issues", requiresAdmin: false, enabled: this.issuesEnabled },
      { id: "nav-userManagement", name: "NavigationBar.UserManagement", icon: "fas fa-users", link: "/usermanagement", requiresAdmin: true, enabled: true },
      //id: "",  { name: "NavigationBar.Calendar", icon: "calendar_today", link: "/calendar", requiresAdmin: false, enabled: true },
      { id: "nav-adminDonate", name: "NavigationBar.Donate", icon: "fas fa-dollar-sign", link: "https://www.paypal.me/PlexRequestsNet", externalLink: true, requiresAdmin: true, enabled: true, toolTip: true, style: "color:red;", toolTipMessage: 'NavigationBar.DonateTooltip' },
      { id: "nav-userDonate", name: "NavigationBar.Donate", icon: "fas fa-dollar-sign", link: customizationSettings.customDonationUrl, externalLink: true, requiresAdmin: false, enabled: customizationSettings.enableCustomDonations, toolTip: true, toolTipMessage: customizationSettings.customDonationMessage },
      { id: "nav-featureSuggestion", name: "NavigationBar.FeatureSuggestion", icon: "far fa-lightbulb", link: "https://features.ombi.io/", externalLink: true, requiresAdmin: true, enabled: true, toolTip: true, toolTipMessage: 'NavigationBar.FeatureSuggestionTooltip'},
      { id: "nav-settings", name: "NavigationBar.Settings", icon: "fas fa-cogs", link: "/Settings/About", requiresAdmin: true, enabled: true },
      ];
  }

  public logOut() {
    this.logoutClick.emit();
  }

  public changeFilter(event: MatSlideToggleChange, searchFilterType: SearchFilterType) {
    switch (searchFilterType) {
      case SearchFilterType.Movie:
        this.searchFilter.movies = event.checked;
        break;
      case SearchFilterType.TvShow:
        this.searchFilter.tvShows = event.checked;
        break;
      case SearchFilterType.Music:
        this.searchFilter.music = event.checked;
        break;
      case SearchFilterType.People:
        this.searchFilter.people = event.checked;
        break;
    }
    this.filterService.changeFilter(this.searchFilter);
    this.store.save("searchFilter", JSON.stringify(this.searchFilter));
  }

  public openAdvancedSearch() {
    const dialogRef = this.dialogService.open(AdvancedSearchDialogComponent, { panelClass: 'dialog-responsive' });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.router.navigate([`discover/advanced/search`]);
      }

      return;
    });
  }

  public getUserImage(): string {
    var fallback = this.applicationLogo ? this.applicationLogo : 'https://raw.githubusercontent.com/Ombi-app/Ombi/gh-pages/img/android-chrome-512x512.png';
    return `https://www.gravatar.com/avatar/${this.emailHash}?d=${fallback}`;
  }

  public openMobileApp(event: any) {
    event.preventDefault();

    const url = `ombi://${this.applicationUrl}|${this.accessToken}`;
    window.location.assign(url);
}

}
