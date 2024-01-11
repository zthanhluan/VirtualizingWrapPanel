using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

//Luan.VT convert from newest net core to net core 2.2
//https://github.com/sbaeumlisberger/VirtualizingWrapPanel/tree/v2.0.0

namespace ZTL
{
    /// <summary>
    /// Provides the size of items displayed in an VirtualizingPanel.
    /// </summary>
    public interface IItemSizeProvider
    {
        /// <summary>
        /// Gets the size for the specified item.
        /// </summary>
        Size GetSizeForItem(object item);
    }
}