// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Symbol_Mapper_Project.Models;
using System;
using System.Collections;
using Windows.System;
using Windows.UI.Core;

namespace Symbol_Mapper_Project.Components
{
    public class SearchboxQuerySubmittedEventArgs : EventArgs
    {
        public object SelectedItem;
    }

    public sealed partial class Searchbox : UserControl
    {
        private string searchbox_last_value = string.Empty;

        public event EventHandler<TextChangedEventArgs> TextChanged;
        public event EventHandler<SearchboxQuerySubmittedEventArgs> QuerySubmitted;

        public event EventHandler<RoutedEventArgs> FocusGot;
        public event EventHandler<RoutedEventArgs> FocusLost;

        public string Text
        {
            get 
            { 
                return searchbox.Text; 
            }
            set 
            { 
                searchbox.Text = value;

                if (value == string.Empty)
                {
                    searchbox_last_value = string.Empty;
                }
            }
        }

        public ListView ListView
        {
            get
            {
                return search_display;
            }
        }

        public Searchbox()
        {
            this.InitializeComponent();
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Get shift and tab state to use the key combination `SHIFT + TAB` to move the list up
            CoreVirtualKeyStates shift_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            bool shift_down = shift_state == CoreVirtualKeyStates.Down || shift_state == (CoreVirtualKeyStates.Down | CoreVirtualKeyStates.Locked);

            CoreVirtualKeyStates tab_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Tab);
            bool tab_down = tab_state == CoreVirtualKeyStates.Down || tab_state == (CoreVirtualKeyStates.Down | CoreVirtualKeyStates.Locked);
            
            // Implement keyboard movement
            // and don't allow one SPACE as the first character
            if (e.Key == VirtualKey.Up ||
               (tab_down &&
                shift_down))
            {
                e.Handled = true;
                MoveListUp();
            }
            else if (e.Key == VirtualKey.Down ||
                    (tab_down &&
                    !shift_down))
            {
                e.Handled = true;
                MoveListDown();
            }
            else if (e.Key == VirtualKey.Space &&
                     searchbox.Text.Length == 0)
            {
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                SearchboxQuerySubmittedEventArgs args = new()
                {
                    SelectedItem = search_display.SelectedItem
                };

                OnQuerySubmitted(args);
            }
            else if (e.Key == VirtualKey.Shift)
            {
                e.Handled = true;
            }
            else
            {
                search_display.SelectedIndex = -1;
            }
        }
        
        private void MoveListDown()
        {
            int last_index = search_display.SelectedIndex;

            if (last_index == -1)
            {
                searchbox_last_value = searchbox.Text;
            }

            int list_len = (search_display.ItemsSource as IList).Count;

            if (list_len > 0)
            {
                int next_index = (last_index + 1) % list_len;

                search_display.SelectedIndex = next_index;

                searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
                search_display.ScrollIntoView(search_display.SelectedItem);

                if (next_index == 0 && (last_index + 1) > 0)
                {
                    search_display.SelectedIndex = -1;
                    searchbox.Text = searchbox_last_value;
                }
            }
            else
            {
                search_display.SelectedIndex = -1;
            }

            searchbox.SelectionStart = searchbox.Text.Length;
            searchbox.SelectionLength = 0;
        }

        private void MoveListUp()
        {
            int last_index = search_display.SelectedIndex;
            int next_index = last_index - 1;

            if (search_display.SelectedIndex == 0)
            {
                search_display.SelectedIndex = -1;

                searchbox.Text = searchbox_last_value;
            }
            else if (next_index < -1)
            {
                int list_len = (search_display.ItemsSource as IList).Count;

                search_display.SelectedIndex = list_len - 1;
                search_display.ScrollIntoView(search_display.SelectedItem);

                searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
            }
            else
            {
                search_display.SelectedIndex = next_index;
                search_display.ScrollIntoView(search_display.SelectedItem);

                searchbox.Text = (search_display.SelectedItem as UnicodeData).UnicodeCharacter;
            }

            searchbox.SelectionStart = searchbox.Text.Length;
            searchbox.SelectionLength = 0;
        }

        private void OnTextChanged(object _, TextChangedEventArgs e)
        {
            if (search_display.SelectedIndex == -1 &&
                (searchbox.Text == string.Empty ||
                searchbox.Text != searchbox_last_value))
            {
                TextChanged?.Invoke(this, e);
            }
        }

        private void OnItemClicked(object _, ItemClickEventArgs e)
        {
            search_display.SelectedItem = e.ClickedItem;

            SearchboxQuerySubmittedEventArgs args = new()
            {
                SelectedItem = e.ClickedItem
            };

            OnQuerySubmitted(args);
        }

        private void OnQuerySubmitted(SearchboxQuerySubmittedEventArgs e)
        {
            QuerySubmitted?.Invoke(this, e);
        }

        private void OnFocusGot(object _, RoutedEventArgs e)
        {
            FocusGot?.Invoke(this, e);
        }

        private void OnFocusLost(object _, RoutedEventArgs e)
        {
            FocusLost?.Invoke(this, e);
        }
    }
}
