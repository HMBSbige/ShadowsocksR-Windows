using Shadowsocks.Util;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Shadowsocks.Controls
{
    public class TreeViewEx : TreeView
    {
        public TreeViewEx()
        {
            SelectedItemChanged += TreeViewEx_SelectedItemChanged;
            Focusable = true;
        }

        public readonly HashSet<TreeViewItem> SelectedItems = new HashSet<TreeViewItem>();

        private static bool CtrlPressed => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        private void Deselect(TreeViewItem treeViewItem)
        {
            treeViewItem.Background = Brushes.White;
            treeViewItem.Foreground = Brushes.Black;
            treeViewItem.IsSelected = false;
            SelectedItems.Remove(treeViewItem);
        }

        private void ChangeSelectedState(TreeViewItem treeViewItem)
        {
            if (!SelectedItems.Contains(treeViewItem))
            {
                treeViewItem.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xd7));
                treeViewItem.Foreground = Brushes.White;
                treeViewItem.IsSelected = true;
                SelectedItems.Add(treeViewItem);
            }
            else
            {
                Deselect(treeViewItem);
            }
        }

        private void TreeViewEx_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var treeViewItem = ViewUtils.GetTreeViewItem(ItemContainerGenerator, SelectedItem);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();

                if (!CtrlPressed)
                {
                    var selectedTreeViewItemList = SelectedItems.ToArray();

                    foreach (var treeViewItem1 in selectedTreeViewItemList)
                    {
                        Deselect(treeViewItem1);
                    }
                }

                ChangeSelectedState(treeViewItem);
            }
        }
    }
}
