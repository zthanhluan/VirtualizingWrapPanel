﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

//Luan.VT convert from newest net core to net core 2.2
//https://github.com/sbaeumlisberger/VirtualizingWrapPanel/tree/v2.0.0

namespace ZTL
{
    /// <summary>
    /// A implementation of a wrap panel that supports virtualization and can be used in horizontal and vertical orientation.
    /// </summary>
    public class VirtualizingWrapPanel : VirtualizingPanelBase
    {
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, (obj, args) => ((VirtualizingWrapPanel)obj).Orientation_Changed()));

        public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(nameof(ItemSize), typeof(Size), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Size.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, (obj, args) => ((VirtualizingWrapPanel)obj).ItemSize_Changed()));

        public static readonly DependencyProperty AllowDifferentSizedItemsProperty = DependencyProperty.Register(nameof(AllowDifferentSizedItems), typeof(bool), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure, (obj, args) => ((VirtualizingWrapPanel)obj).AllowDifferentSizedItems_Changed()));

        public static readonly DependencyProperty ItemSizeProviderProperty = DependencyProperty.Register(nameof(ItemSizeProvider), typeof(IItemSizeProvider), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SpacingModeProperty = DependencyProperty.Register(nameof(SpacingMode), typeof(SpacingMode), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(SpacingMode.Uniform, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register(nameof(StretchItems), typeof(bool), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// Gets or sets a value that specifies the orientation in which items are arranged before wrapping. The default value is <see cref="Orientation.Horizontal"/>.
        /// </summary>
        public Orientation Orientation { get { return (Orientation)GetValue(OrientationProperty); } set { SetValue(OrientationProperty, value); } }

        /// <summary>
        /// Gets or sets a value that specifies the size of the items. The default value is <see cref="Size.Empty"/>. 
        /// If the value is <see cref="Size.Empty"/> the item size is determined by measuring the first realized item.
        /// </summary>
        public Size ItemSize { get { return (Size)GetValue(ItemSizeProperty); } set { SetValue(ItemSizeProperty, value); } }

        /// <summary>
        /// Specifies whether items can have different sizes. The default value is false. If this property is enabled, 
        /// it is strongly recommended to also set the <see cref="ItemSizeProvider"/> property. Otherwise, the position 
        /// of the items is not always guaranteed to be correct.
        /// </summary>
        public bool AllowDifferentSizedItems { get { return (bool)GetValue(AllowDifferentSizedItemsProperty); } set { SetValue(AllowDifferentSizedItemsProperty, value); } }

        /// <summary>
        /// Specifies an instance of <see cref="IItemSizeProvider"/> which provides the size of the items. In order to allow
        /// different sized items, also enable the <see cref="AllowDifferentSizedItems"/> property.
        /// </summary>
        public IItemSizeProvider ItemSizeProvider { get { return (IItemSizeProvider)GetValue(ItemSizeProviderProperty); } set { SetValue(ItemSizeProviderProperty, value); } }

        /// <summary>
        /// Gets or sets the spacing mode used when arranging the items. The default value is <see cref="SpacingMode.Uniform"/>.
        /// </summary>
        public SpacingMode SpacingMode { get { return (SpacingMode)GetValue(SpacingModeProperty); } set { SetValue(SpacingModeProperty, value); } }

        /// <summary>
        /// Gets or sets a value that specifies if the items get stretched to fill up remaining space. The default value is false.
        /// </summary>
        /// <remarks>
        /// The MaxWidth and MaxHeight properties of the ItemContainerStyle can be used to limit the stretching. 
        /// In this case the use of the remaining space will be determined by the SpacingMode property. 
        /// </remarks>
        public bool StretchItems { get { return (bool)GetValue(StretchItemsProperty); } set { SetValue(StretchItemsProperty, value); } }

        /// <summary>
        /// Gets value that indicates whether the <see cref="VirtualizingPanel"/> can virtualize items 
        /// that are grouped or organized in a hierarchy.
        /// </summary>
        /// <returns>always true for <see cref="VirtualizingWrapPanel"/></returns>
        protected override bool CanHierarchicallyScrollAndVirtualizeCore => true;

        protected override bool HasLogicalOrientation => false;

        protected override Orientation LogicalOrientation => Orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;

        private static readonly Size InfiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

        private static readonly Size FallbackItemSize = new Size(48, 48);

        private ItemContainerManager ItemContainerManager
        {
            get
            {
                if (_itemContainerManager == null)
                {
                    _itemContainerManager = new ItemContainerManager(
                        ItemContainerGenerator,
                        AddInternalChild,
                        child => RemoveInternalChildRange(InternalChildren.IndexOf(child), 1));
                    _itemContainerManager.ItemsChanged += ItemContainerManager_ItemsChanged;
                }
                return _itemContainerManager;
            }
        }
        private ItemContainerManager _itemContainerManager;

        /// <summary>
        /// The cache length before and after the viewport. 
        /// </summary>
        private VirtualizationCacheLength cacheLength;
        /// <summary>
        /// The Unit of the cache length. Can be Pixel, Item or Page. 
        /// When the ItemsOwner is a group item it can only be pixel or item.
        /// </summary>
        private VirtualizationCacheLengthUnit cacheLengthUnit;

        private Size? sizeOfFirstItem;

        private readonly Dictionary<object, Size> itemSizesCache = new Dictionary<object, Size>();
        private Size? averageItemSizeCache;

        private int itemsInKnownExtend = 0;

        private int startItemIndex = -1;
        private int endItemIndex = -1;

        private double startItemOffsetX = 0;
        private double startItemOffsetY = 0;

        private double knownExtendX = 0;
        private double knownExtendY = 0;

        private int bringIntoViewItemIndex = -1;
        private FrameworkElement bringIntoViewContainer;

        public void ClearItemSizeCache()
        {
            itemSizesCache.Clear();
            averageItemSizeCache = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ShouldIgnoreMeasure())
            {
                return DesiredSize;
            }

            ItemContainerManager.IsRecycling = IsRecycling;

            MeasureBringIntoViewContainer(InfiniteSize);

            Size newViewportSize;

            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
            {
                IHierarchicalVirtualizationAndScrollInfo groupItem = ItemsOwner as IHierarchicalVirtualizationAndScrollInfo;
                ScrollOffset = groupItem.Constraints.Viewport.Location;
                newViewportSize = GetViewportSizeFromGroupItem(groupItem);
                cacheLength = groupItem.Constraints.CacheLength;
                cacheLengthUnit = groupItem.Constraints.CacheLengthUnit;
            }
            else
            {
                newViewportSize = availableSize;
                cacheLength = GetCacheLength(ItemsOwner);
                cacheLengthUnit = GetCacheLengthUnit(ItemsOwner);
            }

            averageItemSizeCache = null;

            UpdateViewportSize(newViewportSize);
            RealizeAndVirtualizeItems();
            UpdateExtent();

            double desiredWidth = Math.Min(availableSize.Width, Extent.Width);
            double desiredHeight = Math.Min(availableSize.Height, Extent.Height);

            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
            {
                desiredWidth = Math.Max(desiredWidth, newViewportSize.Width);
                desiredHeight = Math.Max(desiredHeight, newViewportSize.Height);
            }

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ViewportSize = finalSize;

            ArrangeBringIntoViewContainer();

            foreach (var cachedContainer in ItemContainerManager.CachedContainers)
            {
                cachedContainer.Arrange(new Rect(0, 0, 0, 0));
            }

            if (startItemIndex == -1)
            {
                return finalSize;
            }

            bool hierarchical = ItemsOwner is IHierarchicalVirtualizationAndScrollInfo;
            double x = startItemOffsetX + GetX(ScrollOffset);
            double y = hierarchical ? startItemOffsetY : startItemOffsetY - GetY(ScrollOffset);
            double rowHeight = 0;
            var rowChilds = new List<UIElement>();
            var childSizes = new List<Size>();

            for (int i = startItemIndex; i <= endItemIndex; i++)
            {
                var item = Items[i];
                var child = ItemContainerManager.RealizedContainers[item];

                Size? upfrontKnownItemSize = GetUpfrontKnownItemSize(item);

                Size childSize = upfrontKnownItemSize ?? itemSizesCache[item];

                if (rowChilds.Count > 0 && x + GetWidth(childSize) > GetWidth(finalSize))
                {
                    ArrangeRow(GetWidth(finalSize), rowChilds, childSizes, y, hierarchical);
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                    rowChilds.Clear();
                    childSizes.Clear();
                }

                x += GetWidth(childSize);
                rowHeight = Math.Max(rowHeight, GetHeight(childSize));
                rowChilds.Add(child);
                childSizes.Add(childSize);
            }

            if (rowChilds.Any())
            {
                ArrangeRow(GetWidth(finalSize), rowChilds, childSizes, y, hierarchical);
            }

            return finalSize;
        }

        protected override void BringIndexIntoView(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"The argument {nameof(index)} must be >= 0 and < the count of items.");
            }

            var container = (FrameworkElement)ItemContainerManager.Realize(index);

            bringIntoViewItemIndex = index;
            bringIntoViewContainer = container;

            // make sure the container is measured and arranged before calling BringIntoView        
            InvalidateMeasure();
            UpdateLayout();

            container.BringIntoView();
        }

        private void ItemContainerManager_ItemsChanged(object sender, ItemContainerManagerItemsChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove
                || e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var key in itemSizesCache.Keys.Except(Items).ToList())
                {
                    itemSizesCache.Remove(key);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                itemSizesCache.Clear();
            }

            itemsInKnownExtend = 0; // force recalucaltion of extend
        }

        private void Orientation_Changed()
        {
            MouseWheelScrollDirection = Orientation == Orientation.Horizontal
                                        ? ScrollDirection.Vertical
                                        : ScrollDirection.Horizontal;
            SetVerticalOffset(0);
            SetHorizontalOffset(0);
        }

        private void AllowDifferentSizedItems_Changed()
        {
            foreach (var child in InternalChildren.Cast<UIElement>())
            {
                child.InvalidateMeasure();
            }
        }

        private void ItemSize_Changed()
        {
            foreach (var child in InternalChildren.Cast<UIElement>())
            {
                child.InvalidateMeasure();
            }
        }

        private Size GetViewportSizeFromGroupItem(IHierarchicalVirtualizationAndScrollInfo groupItem)
        {
            double viewportWidth = Math.Max(groupItem.Constraints.Viewport.Size.Width, 0);
            double viewporteHeight = Math.Max(groupItem.Constraints.Viewport.Size.Height, 0);

            if (VisualTreeHelper.GetParent(this) is ItemsPresenter)
            {
                ItemsPresenter itemsPresenter = VisualTreeHelper.GetParent(this) as ItemsPresenter;
                var margin = itemsPresenter.Margin;

                if (Orientation == Orientation.Horizontal)
                {
                    viewportWidth = Math.Max(0, viewportWidth - (margin.Left + margin.Right));
                }
                else
                {
                    viewporteHeight = Math.Max(0, viewporteHeight - (margin.Top + margin.Bottom));
                }
            }

            if (Orientation == Orientation.Vertical)
            {
                viewporteHeight = Math.Max(0, viewporteHeight - groupItem.HeaderDesiredSizes.PixelSize.Height);
            }

            return new Size(viewportWidth, viewporteHeight);
        }

        private void MeasureBringIntoViewContainer(Size availableSize)
        {
            if (bringIntoViewContainer != null && !bringIntoViewContainer.IsMeasureValid)
            {
                bringIntoViewContainer.Measure(GetUpfrontKnownItemSize(Items[bringIntoViewItemIndex]) ?? availableSize);
                if (sizeOfFirstItem == null)
                {
                    sizeOfFirstItem = bringIntoViewContainer.DesiredSize;
                }
            }
        }

        private void ArrangeBringIntoViewContainer()
        {
            if (bringIntoViewContainer != null)
            {
                bool hierarchical = ItemsOwner is IHierarchicalVirtualizationAndScrollInfo;
                var offset = FindItemOffset(bringIntoViewItemIndex);
                offset = new Point(offset.X - GetX(ScrollOffset), hierarchical ? offset.Y : offset.Y - GetY(ScrollOffset));
                var size = GetUpfrontKnownItemSize(Items[bringIntoViewItemIndex]) ?? bringIntoViewContainer.DesiredSize;
                bringIntoViewContainer.Arrange(new Rect(offset, size));
            }
        }

        private void RealizeAndVirtualizeItems()
        {
            FindStartIndexAndOffset();
            VirtualizeItemsBeforeStartIndex();
            RealizeItemsAndFindEndIndex();
            VirtualizeItemsAfterEndIndex();
        }

        private Size GetAverageItemSize()
        {
            if (ItemSize != Size.Empty)
            {
                return ItemSize;
            }
            else if (!AllowDifferentSizedItems)
            {
                return sizeOfFirstItem ?? FallbackItemSize;
            }
            else
            {
                if (averageItemSizeCache == null && itemSizesCache.Values.Any())
                {
                    averageItemSizeCache = CalculateAverageSize(itemSizesCache.Values);
                }
                return averageItemSizeCache ?? FallbackItemSize;
            }
        }

        private Point FindItemOffset(int itemIndex)
        {
            double x = 0, y = 0, rowHeight = 0;

            for (int i = 0; i <= itemIndex; i++)
            {
                Size itemSize = GetAssumedItemSize(Items[i]);

                if (x != 0 && x + GetWidth(itemSize) > GetWidth(ViewportSize))
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }

                if (i != itemIndex)
                {
                    x += GetWidth(itemSize);
                    rowHeight = Math.Max(rowHeight, GetHeight(itemSize));
                }
            }

            return CreatePoint(x, y);
        }

        private void UpdateViewportSize(Size newViewportSize)
        {
            // Retain the current viewport size if the new viewport size
            // received from the parent virtualizing panel is zero. This 
            // is necessary for the BringIndexIntoView function to work.
            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo
                && newViewportSize.Width == 0
                && newViewportSize.Height == 0)
            {
                return;
            }

            if (GetWidth(newViewportSize) != GetWidth(ViewportSize))
            {
                knownExtendY = 0;
            }

            if (newViewportSize != ViewportSize)
            {
                ViewportSize = newViewportSize;
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        private void FindStartIndexAndOffset()
        {
            if (ViewportSize.Width == 0 && ViewportSize.Height == 0)
            {
                startItemIndex = -1;
                startItemOffsetX = 0;
                startItemOffsetY = 0;
                return;
            }

            double startOffsetY = DetermineStartOffsetY();

            if (startOffsetY <= 0)
            {
                startItemIndex = 0;
                startItemOffsetX = 0;
                startItemOffsetY = 0;
                return;
            }

            double x = 0, y = 0, rowHeight = 0;
            int indexOfFirstRowItem = 0;

            int itemIndex = 0;
            foreach (var item in Items)
            {
                Size itemSize = GetAssumedItemSize(item);

                if (x + GetWidth(itemSize) > GetWidth(ViewportSize) && x != 0)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                    indexOfFirstRowItem = itemIndex;
                }
                x += GetWidth(itemSize);
                rowHeight = Math.Max(rowHeight, GetHeight(itemSize));

                if (y + rowHeight > startOffsetY)
                {
                    if (cacheLengthUnit == VirtualizationCacheLengthUnit.Item)
                    {
                        startItemIndex = Math.Max(indexOfFirstRowItem - (int)cacheLength.CacheBeforeViewport, 0);
                        var itemOffset = FindItemOffset(startItemIndex);
                        startItemOffsetX = GetX(itemOffset);
                        startItemOffsetY = GetY(itemOffset);
                    }
                    else
                    {
                        startItemIndex = indexOfFirstRowItem;
                        startItemOffsetX = 0;
                        startItemOffsetY = y;
                    }
                    break;
                }

                itemIndex++;
            }

            // make sure that at least one item is realized to allow correct calculation of the extend
            if (startItemIndex == -1 && Items.Count > 0)
            {
                startItemIndex = Items.Count - 1;
                startItemOffsetX = 0;
                startItemOffsetY = 0;
            }
        }

        private void RealizeItemsAndFindEndIndex()
        {
            if (startItemIndex == -1)
            {
                endItemIndex = -1;
                knownExtendX = 0;
                knownExtendY = 0;
                itemsInKnownExtend = 0;
                return;
            }

            int newEndItemIndex = Items.Count - 1;
            bool endItemIndexFound = false;

            double endOffsetY = DetermineEndOffsetY();

            double x = startItemOffsetX;
            double y = startItemOffsetY;
            double rowHeight = 0;

            knownExtendX = 0;

            for (int itemIndex = startItemIndex; itemIndex <= newEndItemIndex; itemIndex++)
            {
                if (itemIndex == 0)
                {
                    sizeOfFirstItem = null;
                }

                object item = Items[itemIndex];

                var container = ItemContainerManager.Realize(itemIndex);

                if (container == bringIntoViewContainer)
                {
                    bringIntoViewItemIndex = -1;
                    bringIntoViewContainer = null;
                }

                Size? upfrontKnownItemSize = GetUpfrontKnownItemSize(item);

                container.Measure(upfrontKnownItemSize ?? InfiniteSize);

                var containerSize = DetermineContainerSize(item, container, upfrontKnownItemSize);

                if (AllowDifferentSizedItems == false && sizeOfFirstItem == null)
                {
                    sizeOfFirstItem = containerSize;
                }

                if (x != 0 && x + GetWidth(containerSize) > GetWidth(ViewportSize))
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }

                x += GetWidth(containerSize);
                knownExtendX = Math.Max(x, knownExtendX);
                rowHeight = Math.Max(rowHeight, GetHeight(containerSize));

                if (endItemIndexFound == false)
                {
                    if (y >= endOffsetY
                        || (AllowDifferentSizedItems == false
                            && x + GetWidth(sizeOfFirstItem.Value) > GetWidth(ViewportSize)
                            && y + rowHeight >= endOffsetY))
                    {
                        endItemIndexFound = true;

                        newEndItemIndex = itemIndex;

                        if (cacheLengthUnit == VirtualizationCacheLengthUnit.Item)
                        {
                            newEndItemIndex = Math.Min(newEndItemIndex + (int)cacheLength.CacheAfterViewport, Items.Count - 1);
                            // loop continues unitl newEndItemIndex is reached
                        }
                    }
                }
            }

            endItemIndex = newEndItemIndex;
            knownExtendY = Math.Max(y + rowHeight, knownExtendY);
            itemsInKnownExtend = Math.Max(endItemIndex + 1, itemsInKnownExtend);
        }  

        private Size DetermineContainerSize(object item, UIElement container, Size? upfrontKnownItemSize)
        {
            if (AllowDifferentSizedItems)
            {
                if (upfrontKnownItemSize != null)
                    {
                    return upfrontKnownItemSize.Value;
                }
                itemSizesCache[item] = container.DesiredSize;
                return container.DesiredSize;
            }
            else
            {
                return upfrontKnownItemSize ?? container.DesiredSize;
            }
        }

        private void VirtualizeItemsBeforeStartIndex()
        {
            var containers = ItemContainerManager.RealizedContainers.Values.ToList();
            foreach (var container in containers.Where(container => container != bringIntoViewContainer))
            {
                int itemIndex = ItemContainerManager.FindItemIndexOfContainer(container);

                if (itemIndex < startItemIndex)
                {
                    ItemContainerManager.Virtualize(container);
                }
            }
        }

        private void VirtualizeItemsAfterEndIndex()
        {
            var containers = ItemContainerManager.RealizedContainers.Values.ToList();
            foreach (var container in containers.Where(container => container != bringIntoViewContainer))
            {
                int itemIndex = ItemContainerManager.FindItemIndexOfContainer(container);

                if (itemIndex > endItemIndex)
                {
                    ItemContainerManager.Virtualize(container);
                }
            }
        }

        private void UpdateExtent()
        {
            Size extent;

            if (itemsInKnownExtend == 0)
            {
                extent = new Size(0, 0);
            }
            else if (!AllowDifferentSizedItems)
            {
                if (ItemSize != Size.Empty)
                {
                    extent = CalculateExtentForSameSizeItems(ItemSize);
                }
                else
                {
                    extent = CalculateExtentForSameSizeItems(sizeOfFirstItem.Value);
                }
            }
            else
            {
                double estimatedExtend = ((double)Items.Count / itemsInKnownExtend) * knownExtendY;
                extent = CreateSize(knownExtendX, estimatedExtend);
            }

            if (extent != Extent)
            {
                Extent = extent;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (GetY(ScrollOffset) + GetHeight(ViewportSize) > GetHeight(Extent))
            {
                ScrollOffset = CreatePoint(GetX(ScrollOffset), Math.Max(0, GetHeight(Extent) - GetHeight(ViewportSize)));
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        private Size CalculateExtentForSameSizeItems(Size itemSize)
        {
            int itemsPerRow = (int)Math.Max(1, Math.Floor(GetWidth(ViewportSize) / GetWidth(itemSize)));
            double extentY = Math.Ceiling(((double)Items.Count) / itemsPerRow) * GetHeight(itemSize);
            return CreateSize(knownExtendX, extentY);
        }

        private double DetermineStartOffsetY()
        {
            double cacheLength = 0;

            if (cacheLengthUnit == VirtualizationCacheLengthUnit.Page)
            {
                cacheLength = this.cacheLength.CacheBeforeViewport * GetHeight(ViewportSize);
            }
            else if (cacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
            {
                cacheLength = this.cacheLength.CacheBeforeViewport;
            }

            return Math.Max(GetY(ScrollOffset) - cacheLength, 0);
        }

        private double DetermineEndOffsetY()
        {
            double cacheLength = 0;

            if (cacheLengthUnit == VirtualizationCacheLengthUnit.Page)
            {
                cacheLength = this.cacheLength.CacheAfterViewport * GetHeight(ViewportSize);
            }
            else if (cacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
            {
                cacheLength = this.cacheLength.CacheAfterViewport;
            }

            return Math.Max(GetY(ScrollOffset), 0) + GetHeight(ViewportSize) + cacheLength;
        }

        private Size? GetUpfrontKnownItemSize(object item)
        {
            if (ItemSize != Size.Empty)
            {
                return ItemSize;
            }
            if (!AllowDifferentSizedItems && sizeOfFirstItem != null)
            {
                return sizeOfFirstItem;
            }
            if (ItemSizeProvider != null)
            {
                var size = ItemSizeProvider.GetSizeForItem(item);
                itemSizesCache[item] = size;
                return size;
            }
            return null;
        }

        private Size GetAssumedItemSize(object item)
        {
            Size? upfrontKnownItemSize = GetUpfrontKnownItemSize(item);

            if (upfrontKnownItemSize.HasValue)
            {
                return upfrontKnownItemSize.Value;
            }

            Size cachedItemSize;
            if (itemSizesCache.TryGetValue(item, out cachedItemSize))
            {
                return cachedItemSize;
            }

            return GetAverageItemSize();
        }


        private void ArrangeRow(double rowWidth, List<UIElement> children, List<Size> childSizes, double y, bool hierarchical)
        {
            double summedUpChildWidth;
            double extraWidth = 0;

            if (AllowDifferentSizedItems)
            {
                summedUpChildWidth = childSizes.Sum(childSize => GetWidth(childSize));

                if (StretchItems)
                {
                    double unusedWidth = rowWidth - summedUpChildWidth;
                    extraWidth = unusedWidth / children.Count;
                    summedUpChildWidth = rowWidth;
                }
            }
            else
            {
                double childWidth = GetWidth(childSizes[0]);
                int itemsPerRow = (int)Math.Max(Math.Floor(rowWidth / childWidth), 1);

                if (StretchItems)
                {
                    var firstChild = (FrameworkElement)children[0];
                    double maxWidth = Orientation == Orientation.Horizontal ? firstChild.MaxWidth : firstChild.MaxHeight;
                    double stretchedChildWidth = Math.Min(rowWidth / itemsPerRow, maxWidth);
                    stretchedChildWidth = Math.Max(stretchedChildWidth, childWidth); // ItemSize might be greater than MaxWidth/MaxHeight
                    extraWidth = stretchedChildWidth - childWidth;
                    summedUpChildWidth = itemsPerRow * stretchedChildWidth;
                }
                else
                {
                    summedUpChildWidth = itemsPerRow * childWidth;
                }
            }

            double innerSpacing = 0;
            double outerSpacing = 0;

            if (summedUpChildWidth < rowWidth)
            {
                CalculateRowSpacing(rowWidth, children, summedUpChildWidth, out innerSpacing, out outerSpacing);
            }

            double x = hierarchical ? outerSpacing : -GetX(ScrollOffset) + outerSpacing;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                Size childSize = childSizes[i];
                child.Arrange(CreateRect(x, y, GetWidth(childSize) + extraWidth, GetHeight(childSize)));
                x += GetWidth(childSize) + extraWidth + innerSpacing;
            }
        }

        private void CalculateRowSpacing(double rowWidth, List<UIElement> children, double summedUpChildWidth, out double innerSpacing, out double outerSpacing)
        {
            int childCount;

            if (AllowDifferentSizedItems)
            {
                childCount = children.Count;
            }
            else
            {
                childCount = (int)Math.Max(1, Math.Floor(rowWidth / GetWidth(sizeOfFirstItem.Value)));
            }

            double unusedWidth = Math.Max(0, rowWidth - summedUpChildWidth);

            switch (SpacingMode)
            {
                case SpacingMode.Uniform:
                    innerSpacing = outerSpacing = unusedWidth / (childCount + 1);
                    break;

                case SpacingMode.BetweenItemsOnly:
                    innerSpacing = unusedWidth / Math.Max(childCount - 1, 1);
                    outerSpacing = 0;
                    break;

                case SpacingMode.StartAndEndOnly:
                    innerSpacing = 0;
                    outerSpacing = unusedWidth / 2;
                    break;

                case SpacingMode.None:
                default:
                    innerSpacing = 0;
                    outerSpacing = 0;
                    break;
            }
        }

        private Size CalculateAverageSize(ICollection<Size> sizes)
        {
            if (sizes.Any())
            {
                return new Size(
                    Math.Round(sizes.Average(size => size.Width)),
                    Math.Round(sizes.Average(size => size.Height)));
            }
            return Size.Empty;
        }

        #region scroll info

        // TODO determine exact scoll amount for item based scrolling when AllowDifferentSizedItems is true

        protected override double GetLineUpScrollAmount()
        {
            return -Math.Min(GetAverageItemSize().Height * ScrollLineDeltaItem, ViewportSize.Height);
        }

        protected override double GetLineDownScrollAmount()
        {
            return Math.Min(GetAverageItemSize().Height * ScrollLineDeltaItem, ViewportSize.Height);
        }

        protected override double GetLineLeftScrollAmount()
        {
            return -Math.Min(GetAverageItemSize().Width * ScrollLineDeltaItem, ViewportSize.Width);
        }

        protected override double GetLineRightScrollAmount()
        {
            return Math.Min(GetAverageItemSize().Width * ScrollLineDeltaItem, ViewportSize.Width);
        }

        protected override double GetMouseWheelUpScrollAmount()
        {
            return -Math.Min(GetAverageItemSize().Height * MouseWheelDeltaItem, ViewportSize.Height);
        }

        protected override double GetMouseWheelDownScrollAmount()
        {
            return Math.Min(GetAverageItemSize().Height * MouseWheelDeltaItem, ViewportSize.Height);
        }

        protected override double GetMouseWheelLeftScrollAmount()
        {
            return -Math.Min(GetAverageItemSize().Width * MouseWheelDeltaItem, ViewportSize.Width);
        }

        protected override double GetMouseWheelRightScrollAmount()
        {
            return Math.Min(GetAverageItemSize().Width * MouseWheelDeltaItem, ViewportSize.Width);
        }

        protected override double GetPageUpScrollAmount()
        {
            return -ViewportSize.Height;
        }

        protected override double GetPageDownScrollAmount()
        {
            return ViewportSize.Height;
        }

        protected override double GetPageLeftScrollAmount()
        {
            return -ViewportSize.Width;
        }

        protected override double GetPageRightScrollAmount()
        {
            return ViewportSize.Width;
        }

        #endregion

        #region orientation aware helper methods

        private double GetX(Point point) => Orientation == Orientation.Horizontal ? point.X : point.Y;
        private double GetY(Point point) => Orientation == Orientation.Horizontal ? point.Y : point.X;
        private double GetWidth(Size size) => Orientation == Orientation.Horizontal ? size.Width : size.Height;
        private double GetHeight(Size size) => Orientation == Orientation.Horizontal ? size.Height : size.Width;
        private Point CreatePoint(double x, double y) => Orientation == Orientation.Horizontal ? new Point(x, y) : new Point(y, x);
        private Size CreateSize(double width, double height) => Orientation == Orientation.Horizontal ? new Size(width, height) : new Size(height, width);
        private Rect CreateRect(double x, double y, double width, double height) => Orientation == Orientation.Horizontal ? new Rect(x, y, width, height) : new Rect(y, x, height, width);

        #endregion
    }
}
