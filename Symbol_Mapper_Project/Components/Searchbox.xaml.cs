// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Symbol_Mapper_Project.Models;
using System;
using System.Collections;
using Windows.System;

namespace Symbol_Mapper_Project.Components
{
    public class SearchboxQuerySubmittedEventArgs : EventArgs
    {
        public object selected_item;
    }

    public sealed partial class Searchbox : UserControl
    {
        private string searchbox_last_value = string.Empty;

        public EventHandler<TextChangedEventArgs> TextChanged;
        public EventHandler<SearchboxQuerySubmittedEventArgs> QuerySubmitted;

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
            // Implement keyboard movement
            // and don't allow one SPACE as the first character
            switch (e.Key)
            {
                case VirtualKey.Down:
                case VirtualKey.Tab:
                    {
                        e.Handled = true;

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

                        break;
                    }

                case VirtualKey.Up:
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

                        break;
                    }

                case VirtualKey.Space:
                    if (searchbox.Text.Length == 0)
                    {
                        e.Handled = true;
                    }
                    break;

                case VirtualKey.Enter:
                    SearchboxQuerySubmittedEventArgs args = new()
                    {
                        selected_item = search_display.SelectedItem
                    };

                    OnQuerySubmitted(args);
                    break;

                default:
                    search_display.SelectedIndex = -1;
                    break;
            }
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
                selected_item = e.ClickedItem
            };

            OnQuerySubmitted(args);
        }

        private void OnQuerySubmitted(SearchboxQuerySubmittedEventArgs e)
        {
            QuerySubmitted?.Invoke(this, e);
        }
    }
}