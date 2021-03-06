//
// DialogViewController.cs
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Copyright 2010, Novell, Inc.
//
// Code licensed under the MIT X11 license
//
// Original code created by Miguel de Icaza for the MonoTouch.Dialog library available at
// https://github.com/migueldeicaza/MonoTouch.Dialog
//

using System;
using MonoTouch.UIKit;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;

namespace MonoTouch.Dialog
{
	public class DialogViewController : UITableViewController
	{
		public UITableViewStyle Style = UITableViewStyle.Grouped;
		UISearchBar searchBar;
		UITableView tableView;
		RefreshTableHeaderView topRefreshView, bottomRefreshView;
		RootElement root;
		bool pushing;
		bool dirty;
		bool reloading;
		
		public DialogViewController(IntPtr ptr):base(ptr){
		}

		/// <summary>
		/// The root element displayed by the DialogViewController, the value can be changed during runtime to update the contents.
		/// </summary>
		public RootElement Root {
			get {
				return root;
			}
			set {
				if (root == value)
					return;
				if (root != null)
					root.Dispose ();
				
				root = value;
				root.TableView = tableView;		
				ReloadData ();
			}
		} 

		EventHandler bottomRefreshRequested;
		/// <summary>
		/// If you assign a handler to this event before the view is shown, the
		/// DialogViewController will have support for pull-to-refresh from bottom UI.
		/// </summary>
		public event EventHandler BottomRefreshRequested {
			add {
				if (tableView != null)
					throw new ArgumentException ("You should set the handler before the controller is shown");
				bottomRefreshRequested += value; 
			}
			remove {
				bottomRefreshRequested -= value;
			}
		}

		EventHandler topRefreshRequested;
		/// <summary>
		/// If you assign a handler to this event before the view is shown, the
		/// DialogViewController will have support for pull-to-refresh UI.
		/// </summary>
		public event EventHandler TopRefreshRequested {
			add {
				if (tableView != null)
					throw new ArgumentException ("You should set the handler before the controller is shown");
				topRefreshRequested += value; 
			}
			remove {
				topRefreshRequested -= value;
			}
		}
		
		// If the value is 1, we are enabled, used in the source for quick computation
		bool enableSearch;
		public bool EnableSearch {
			get {
			   return enableSearch;
			}
			set {
				if (enableSearch == value)
					return;
				
				// After MonoTouch 3.0, we can allow for the search to be enabled/disable
				if (tableView != null)
					throw new ArgumentException ("You should set EnableSearch before the controller is shown");
				enableSearch = value;
			}
		}
		
		public bool EnableSectionIndex { get; set; }
		
		// If set, we automatically scroll the content to avoid showing the search bar until 
		// the user manually pulls it down.
		public bool AutoHideSearch { get; set; }

		public void EnableSearchCancelButton (){
			foreach (UIView subview in searchBar.Subviews) {
				if (subview is UIButton) {
					((UIButton)subview).Enabled = true;
					return;
				}
			}
		}

		public string SearchPlaceholder { get; set; }
			
		/// <summary>
		/// Invoke this method to trigger a data refresh.   
		/// </summary>
		/// <remarks>
		/// This will invoke the RerfeshRequested event handler, the code attached to it
		/// should start the background operation to fetch the data and when it completes
		/// it should call ReloadComplete to restore the control state.
		/// </remarks>
		public void TriggerTopRefresh ()
		{
			TriggerTopRefresh (false);
		}
		
		void TriggerTopRefresh (bool showStatus)
		{
			if (topRefreshRequested == null)
				return;

			if (reloading)
				return;
			
			reloading = true;
			if (topRefreshView != null)
				topRefreshView.SetActivity (true);
			topRefreshRequested (this, EventArgs.Empty);

			if (showStatus && topRefreshView != null){
				UIView.BeginAnimations ("reloadingData");
				UIView.SetAnimationDuration (0.2);
				TableView.ContentInset = new UIEdgeInsets (60, 0, 0, 0);
				UIView.CommitAnimations ();
			}
		}

		public void TriggerBottomRefresh ()
		{
			TriggerBottomRefresh (false);
		}
		
		void TriggerBottomRefresh (bool showStatus)
		{
			if (bottomRefreshRequested == null)
				return;
			
			if (reloading)
				return;
			
			reloading = true;
			if (bottomRefreshView != null)
				bottomRefreshView.SetActivity (true);
			bottomRefreshRequested (this, EventArgs.Empty);
			
			if (showStatus && bottomRefreshView != null){
				UIView.BeginAnimations ("reloadingData");
				UIView.SetAnimationDuration (0.2);
				TableView.ContentInset = new UIEdgeInsets (0, 0, 60, 0);
				UIView.CommitAnimations ();
			}
		}
		
		
		public void PushRootElement(RootElement root){
			InvokeOnMainThread(()=>{
				var newcontroller = (DialogViewController)Activator.CreateInstance(root.RootControllerType, root);
				newcontroller.NavigationItem.Title = "Select";
				ActivateController(newcontroller, this);
			});
		}
		
		/// <summary>
		/// Invoke this method to signal that a reload has completed, this will update the UI accordingly.
		/// </summary>
		public void ReloadComplete ()
		{
			if (topRefreshView != null)
				topRefreshView.LastUpdate = DateTime.Now;
			
			if (bottomRefreshView != null)
				bottomRefreshView.LastUpdate = DateTime.Now;
			if (!reloading)
				return;

			reloading = false;
			if (topRefreshView != null) {
				topRefreshView.SetActivity (false);
				topRefreshView.Flip (false);
				UIView.BeginAnimations ("doneReloading");
				UIView.SetAnimationDuration (0.3f);
				TableView.ContentInset = new UIEdgeInsets (0, 0, 0, 0);
				topRefreshView.SetStatus (RefreshViewStatus.PullToReload);
				UIView.CommitAnimations ();
			}
			if (bottomRefreshView != null) {
				bottomRefreshView.SetActivity (false);
				bottomRefreshView.Flip (false);
				UIView.BeginAnimations ("doneReloading");
				UIView.SetAnimationDuration (0.3f);
				TableView.ContentInset = new UIEdgeInsets (0, 0, 0, 0);
				bottomRefreshView.SetStatus (RefreshViewStatus.PullToReload);
				UIView.CommitAnimations ();
			}
		}
		
		/// <summary>
		/// Controls whether the DialogViewController should auto rotate
		/// </summary>
		public bool Autorotate { get; set; }
		
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return Autorotate || toInterfaceOrientation == UIInterfaceOrientation.Portrait;
		}
		
		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			base.DidRotate (fromInterfaceOrientation);
			ReloadData ();
		}
		
		Section [] originalSections;
		Element [][] originalElements;
		
		/// <summary>
		/// Allows caller to programatically activate the search bar and start the search process
		/// </summary>
		public void StartSearch ()
		{
			if (originalSections != null)
				return;
			
			searchBar.BecomeFirstResponder ();
			originalSections = Root.Sections.ToArray ();
			originalElements = new Element [originalSections.Length][];
			for (int i = 0; i < originalSections.Length; i++)
				originalElements [i] = originalSections [i].Elements.ToArray ();
		}
		
		/// <summary>
		/// Allows the caller to programatically stop searching.
		/// </summary>
		public virtual void FinishSearch ()
		{
			if (originalSections == null)
				return;
			
			Root.Sections = new List<Section> (originalSections);
			originalSections = null;
			originalElements = null;
			searchBar.Text = "";
			searchBar.ResignFirstResponder ();
			ReloadData ();
		}
		
		public delegate void SearchTextEventHandler (object sender, SearchChangedEventArgs args);
		public event SearchTextEventHandler SearchTextChanged;
		
		public virtual void OnSearchTextChanged (string text)
		{
			if (SearchTextChanged != null)
				SearchTextChanged (this, new SearchChangedEventArgs (text));
		}
		                                     
		public void PerformFilter (string text)
		{
			if (originalSections == null)
				return;
			
			OnSearchTextChanged (text);
			
			var newSections = new List<Section> ();
			
			for (int sidx = 0; sidx < originalSections.Length; sidx++){
				Section newSection = null;
				var section = originalSections [sidx];
				Element [] elements = originalElements [sidx];
				
				for (int eidx = 0; eidx < elements.Length; eidx++){
					if (elements [eidx].Matches (text)){
						if (newSection == null){
							newSection = new Section (section.Header, section.Footer){
								FooterView = section.FooterView,
								HeaderView = section.HeaderView
							};
							newSections.Add (newSection);
						}
						newSection.Add (elements [eidx]);
					}
				}
			}
			
			Root.Sections = newSections;
			
			ReloadData ();
		}
		
		public virtual void SearchButtonClicked (string text)
		{
			this.View.EndEditing (true);
		}
			
		class SearchDelegate : UISearchBarDelegate {
			DialogViewController container;
			
			public SearchDelegate (DialogViewController container)
			{
				this.container = container;
			}
			
			public override void OnEditingStarted (UISearchBar searchBar)
			{
				searchBar.SetShowsCancelButton (true, true);
				container.StartSearch ();
			}
			
			public override void OnEditingStopped (UISearchBar searchBar)
			{
				if (searchBar.Text=="")
					searchBar.SetShowsCancelButton (false, true);
				//container.FinishSearch ();
			}
			
			public override void TextChanged (UISearchBar searchBar, string searchText)
			{
				container.PerformFilter (searchText ?? "");
			}
			
			public override void CancelButtonClicked (UISearchBar searchBar)
			{
				searchBar.Text = "";
				searchBar.SetShowsCancelButton (false, true);
				container.FinishSearch ();
				searchBar.ResignFirstResponder ();
			}
			
			public override void SearchButtonClicked (UISearchBar searchBar)
			{
				container.SearchButtonClicked (searchBar.Text);

				if (!String.IsNullOrEmpty(searchBar.Text))
					container.EnableSearchCancelButton ();
			}
		}
		
		public class Source : UITableViewSource {
			const float yboundary = 65;
			protected DialogViewController Container;
			protected RootElement Root;
			bool checkForRefresh;
			
			public Source (DialogViewController container)
			{
				this.Container = container;
				Root = container.root;
			}
			
			public override int RowsInSection (UITableView tableview, int section)
			{
				var s = Root.Sections [section];
				if (s.Elements==null)
					return 0;
				var count = s.Elements.Count;
				
				return count;
			}

			public override string[] SectionIndexTitles (UITableView tableView)
			{
				if (Container.EnableSectionIndex) {
					var values = new List<string>();
					foreach (var section in Root.Sections) {
						if (section.Caption!=null) {
							values.Add(section.Caption);
						}
					}	
					return values.ToArray();
				}
				return null;
			}


			public override int NumberOfSections (UITableView tableView)
			{
				return Root.Sections.Count;
			}

			public override string TitleForHeader (UITableView tableView, int section)
			{
				return Root.Sections [section].Caption;
			}
			
			public override string TitleForFooter (UITableView tableView, int section)
			{
				return Root.Sections [section].Footer;
			}

			public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
			{
				var section = Root.Sections [indexPath.Section];
				var element = section.Elements [indexPath.Row];
				
				var cell = element.GetCell (tableView);
				if (Root.Appearance.CellHighlightedColor != null) {
					cell.SelectedBackgroundView = new UIView { BackgroundColor = Root.Appearance.CellHighlightedColor };
				}
				 
				cell.Hidden= element.Hidden;
				return cell;
			}

			public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
			{
				Container.AccessorySelected(indexPath);
			}

			public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
			{
				Container.Selected (indexPath);
			}			
			
			public override UIView GetViewForHeader (UITableView tableView, int sectionIdx)
			{
				var section = Root.Sections [sectionIdx];
				return section.HeaderView;
			}

			public override float GetHeightForHeader (UITableView tableView, int sectionIdx)
			{
				var section = Root.Sections [sectionIdx];
				if (section.HeaderView == null)
					return -1;
				return section.HeaderView.Frame.Height;
			}

			public override UIView GetViewForFooter (UITableView tableView, int sectionIdx)
			{
				var section = Root.Sections [sectionIdx];
				return section.FooterView;
			}
			
			public override float GetHeightForFooter (UITableView tableView, int sectionIdx)
			{
				var section = Root.Sections [sectionIdx];
				if (section.FooterView == null)
					return -1;
				return section.FooterView.Frame.Height;
			}
			
			#region Pull to Refresh support
			public override void Scrolled (UIScrollView scrollView)
			{
				if (!checkForRefresh)
					return;
				if (Container.reloading)
					return;
				var point = Container.TableView.ContentOffset;

				var topView  = Container.topRefreshView;
				if (topView == null)
					return;
				
				if (topView.IsFlipped && point.Y > -yboundary && point.Y < 0){
					topView.Flip (true);
					topView.SetStatus (RefreshViewStatus.PullToReload);
				} else if (!topView.IsFlipped && point.Y < -yboundary){
					topView.Flip (true);
					topView.SetStatus (RefreshViewStatus.ReleaseToReload);
				}

				var bottomView  = Container.bottomRefreshView;
				if (bottomView!=null && bottomView.Frame!=null && bottomView.Frame.Y!=scrollView.ContentSize.Height)
					bottomView.Frame = new RectangleF(0,scrollView.ContentSize.Height, bottomView.Frame.Width, bottomView.Frame.Height);

				if (bottomView == null)
					return;
				var bottomOffset = this.TableScrollOffset();
				if (bottomView.IsFlipped && point.Y > yboundary && bottomOffset < 0 && bottomOffset >= -yboundary) {
					bottomView.Flip (true);
					bottomView.SetStatus (RefreshViewStatus.PullToReload);
				} else if (!bottomView.IsFlipped && bottomOffset < -yboundary){
					bottomView.Flip (true);
					bottomView.SetStatus (RefreshViewStatus.ReleaseToReload);
				}
			}

			public float TableScrollOffset() {
				float offset = 0;
				var table = Container.TableView;
				if (table.ContentSize.Height < table.Frame.Height) {
					offset = -table.ContentOffset.Y;
				} else {
					offset = (table.ContentSize.Height - table.ContentOffset.Y) - table.Frame.Height;
				}
				
				return offset;
			}

			public override void DraggingStarted (UIScrollView scrollView)
			{
				checkForRefresh = true;
			}
			
			public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
			{
				if (Container.topRefreshView != null) {
					checkForRefresh = false;
					if (Container.TableView.ContentOffset.Y< 0 && Container.TableView.ContentOffset.Y <= -yboundary)
						Container.TriggerTopRefresh (true);
				}

				if (Container.bottomRefreshView != null) {
					checkForRefresh = false;
					if (-this.TableScrollOffset() > yboundary)
						Container.TriggerBottomRefresh (true);
				}
			}
			#endregion
		}
		
		//
		// Performance trick, if we expose GetHeightForRow, the UITableView will
		// probe *every* row for its size;   Avoid this by creating a separate
		// model that is used only when we have items that require resizing
		//
		public class SizingSource : Source {
			public SizingSource (DialogViewController controller) : base (controller) {}
			
			public override float GetHeightForRow (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
			{
				var section = Root.Sections [indexPath.Section];
				var element = section.Elements [indexPath.Row];
				
				if (element.Hidden) 
					return 0;
				var sizable = element as IElementSizing;
				if (sizable == null)
					return tableView.RowHeight;
				return sizable.GetHeight (tableView, indexPath);
			}
		}
			
		/// <summary>
		/// Activates a nested view controller from the DialogViewController.   
		/// If the view controller is hosted in a UINavigationController it
		/// will push the result.   Otherwise it will show it as a modal
		/// dialog
		/// </summary>
		public void ActivateController (UIViewController controller, DialogViewController oldController)
		{
			dirty = true;
			
			if (typeof(DialogViewController) == controller.GetType()){
				var dialog= (DialogViewController)controller;
				dialog.TableView.BackgroundColor = oldController.TableView.BackgroundColor;
				dialog.tableView.BackgroundView = oldController.tableView.BackgroundView;
			}
			
			var parent = ParentViewController;
			var nav = parent as UINavigationController;
			
			// We can not push a nav controller into a nav controller
			if (nav != null && !(controller is UINavigationController))
				nav.PushViewController (controller, true);
			else
				PresentModalViewController (controller, true);
		}

		/// <summary>
		/// Dismisses the view controller.   It either pops or dismisses
		/// based on the kind of container we are hosted in.
		/// </summary>
		public void DeactivateController (bool animated)
		{
			var parent = ParentViewController;
			var nav = parent as UINavigationController;
			
			if (nav != null)
				nav.PopViewControllerAnimated (animated);
			else
				DismissModalViewControllerAnimated (animated);
		}

		void SetupSearch ()
		{
			if (enableSearch){
				searchBar = new UISearchBar (new RectangleF (0, 0, tableView.Bounds.Width, 44)) {
					Delegate = new SearchDelegate (this),
					
				};

				if (SearchPlaceholder != null)
					searchBar.Placeholder = this.SearchPlaceholder;
				tableView.TableHeaderView = searchBar;					
			} 
		}

		public virtual void AccessorySelected (NSIndexPath indexPath)
		{
			var section = root.Sections [indexPath.Section];
			var element = section.Elements [indexPath.Row];
			
			element.AccessorySelected (this, tableView, indexPath);
		}


		public virtual void Selected (NSIndexPath indexPath)
		{
			var section = root.Sections [indexPath.Section];
			var element = section.Elements [indexPath.Row];

			element.Selected (this, tableView, indexPath);
		}
		
		public virtual UITableView MakeTableView ()
		{
			var tv = new UITableView (UIScreen.MainScreen.Bounds, Style);
			tv.DelaysContentTouches = false;
			tv.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
			tv.AutosizesSubviews = true;
			tv.DelaysContentTouches = false;
			return tv;
			
		}
		
		public override void LoadView ()
		{
			this.tableView = MakeTableView ();
			
			UpdateSource ();
			View = tableView;
			SetupSearch ();
			ConfigureTableView ();
			
			if (root == null)
				return;
			root.TableView = tableView;

			var endEditingWhenTappingBackgroundGesture = new UITapGestureRecognizer((g)=>this.View.EndEditing(true));
			endEditingWhenTappingBackgroundGesture.CancelsTouchesInView = false;
			//this.TableView.AddGestureRecognizer (endEditingWhenTappingBackgroundGesture);
		}
		
		void ConfigureTableView ()
		{
			if (topRefreshRequested != null){
				var bounds = View.Bounds;
				topRefreshView = MakeRefreshTableHeaderView (new RectangleF (0, -bounds.Height, bounds.Width, bounds.Height), true);
				if (reloading)
					topRefreshView.SetActivity (true);
				TableView.AddSubview (topRefreshView);
			}
			if (bottomRefreshRequested != null){
				var bounds = View.Bounds;
				tableView.LayoutIfNeeded();
				bottomRefreshView = MakeRefreshTableHeaderView (new RectangleF (0, tableView.ContentSize.Height, bounds.Width, bounds.Height), false);
				bottomRefreshView.FromTop = false;
				if (reloading)
					bottomRefreshView.SetActivity (true);
				TableView.AddSubview (bottomRefreshView);
			}
		}
		
		public virtual RefreshTableHeaderView MakeRefreshTableHeaderView (RectangleF rect, bool isTop)
		{
			return new RefreshTableHeaderView (rect, isTop);
		}

		public void ClearSearchBar ()
		{
			searchBar.Text = "";
			searchBar.EndEditing(true);
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);

			if (AutoHideSearch){
				if (enableSearch){
					this.ClearSearchBar();
				}
			}
			
			NavigationItem.HidesBackButton = !pushing;
			
			if (root == null)
				return;
			
			root.Prepare ();

			if (root.Caption != null) {
				NavigationItem.Title = root.Caption;
			}

			if (root.CaptionPrompt != null) {
				this.NavigationItem.Prompt = root.CaptionPrompt;
			}
			if (dirty){
				tableView.ReloadData ();
				dirty = false;
			}

			try {
				tableView.ScrollToRow(root.PathForRadio(root.Value), UITableViewScrollPosition.Middle, false);	
			} catch (Exception){}
		}

		public virtual Source CreateSizingSource (bool unevenRows)
		{
			return unevenRows ? new SizingSource (this) : new Source (this);
		}
		
		Source TableSource;
		
		void UpdateSource ()
		{
			if (root == null)
				return;
			
			TableSource = CreateSizingSource (root.UnevenRows);
			tableView.Source = TableSource;
		}

		public void ReloadData ()
		{
			if (root == null)
				return;

			root.Prepare ();
			if (tableView != null){
				UpdateSource ();
				tableView.ReloadData ();
			}
			dirty = false;
		}
		
		public event EventHandler ViewDissapearing;
		
		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
			if (ViewDissapearing != null)
				ViewDissapearing (this, EventArgs.Empty);
		}

		protected void PrepareRoot (RootElement root)
		{
			this.root = root;
			if (root != null)
				root.Prepare ();
		}
		
		protected DialogViewController (bool pushing) : base(UITableViewStyle.Grouped) {
			this.pushing = pushing;
		}
		
		protected DialogViewController (UITableViewStyle style, bool pushing) : base(style) {
			this.pushing = pushing;
			Style = style;
		}
		
		public DialogViewController (RootElement root) : base (UITableViewStyle.Grouped)
		{
			pushing = true;
			PrepareRoot (root);
		}
		
		public DialogViewController (UITableViewStyle style, RootElement root) : base (style)
		{
			this.pushing = true;
            Style = style;
			PrepareRoot (root);
		}
		
		/// <summary>
		///     Creates a new DialogViewController from a RootElement and sets the push status
		/// </summary>
		/// <param name="root">
		/// The <see cref="RootElement"/> containing the information to render.
		/// </param>
		/// <param name="pushing">
		/// A <see cref="System.Boolean"/> describing whether this is being pushed 
		/// (NavigationControllers) or not.   If pushing is true, then the back button 
		/// will be shown, allowing the user to go back to the previous controller
		/// </param>
		public DialogViewController (RootElement root, bool pushing) : base (UITableViewStyle.Grouped)
		{
			this.pushing = pushing;
			PrepareRoot (root);
		}

		public DialogViewController (UITableViewStyle style, RootElement root, bool pushing) : base (style)
		{
			this.pushing = pushing;
			Style = style;
			PrepareRoot (root);
		}
	}
	
}
