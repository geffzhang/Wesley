﻿using Acr.UserDialogs;
using Wesley.Client.Enums;
using Wesley.Client.Models;
using Wesley.Client.Models.WareHouses;
using Wesley.Client.Pages;
using Wesley.Client.Services;
using Microsoft.AppCenter.Crashes;
using Prism.Navigation;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wesley.Client.ViewModels
{
    public class StockQueryPageViewModel : ViewModelBaseCutom
    {

        private readonly IReportingService _reportingService;
        [Reactive] public ObservableCollection<StockCategoryGroup> StockSeries { get; set; } = new ObservableCollection<StockCategoryGroup>();

        [Reactive] public decimal? TotalAmount { get; internal set; }
        public ReactiveCommand<object, Unit> StockSelected { get; }
        private bool ShowZero { get; set; } = false;
        private bool Disabled { get; set; } = true;


        public StockQueryPageViewModel(INavigationService navigationService,
             IProductService productService,
             IUserService userService,
             ITerminalService terminalService,
             IWareHousesService wareHousesService,
             IAccountingService accountingService,
             IReportingService reportingService,
             IDialogService dialogService
            ) : base(navigationService, productService, terminalService, userService, wareHousesService, accountingService, dialogService)
        {
            Title = "库存查询";
            this.ForceRefresh = true;

            _reportingService = reportingService;

            //搜索
            this.WhenAnyValue(x => x.Filter.SerchKey)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s)
                .Throttle(TimeSpan.FromSeconds(2), RxApp.MainThreadScheduler)
                .Subscribe(s => { ((ICommand)SerchCommand)?.Execute(s); })
                .DisposeWith(DeactivateWith);

            this.SerchCommand = ReactiveCommand.Create<string>(e =>
            {
                if (string.IsNullOrEmpty(Filter.SerchKey))
                {
                    this.Alert("请输入关键字！");
                    return;
                }
                ((ICommand)Load)?.Execute(null);
            });

            this.Load = StockSeriesLoader.Load(async () =>
            {
                //重载时排它
                ItemTreshold = 1;

                try
                {
                    this.StockSeries?.Clear();
                    var pending = new List<StockCategoryGroup>();
                    var results = await GetStockCategoryGroupPage(0, PageSize);
                    if (results != null && results.Any())
                    {
                        foreach (var item in results)
                        {
                            if (pending?.Count(s => s.CategoryName == item.CategoryName) == 0)
                            {
                                pending.Add(item);
                            }
                        }
                        this.TotalAmount = pending?.Select(p => p.SubCostAmount).Sum();

                        if (pending.Any())
                            this.StockSeries = new ObservableRangeCollection<StockCategoryGroup>(pending);
                    }
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex);
                }

                return StockSeries;
            });


            //以增量方式加载数据
            this.ItemTresholdReachedCommand = ReactiveCommand.Create(async () =>
            {
                using (var dig = UserDialogs.Instance.Loading("加载中..."))
                {
                    try
                    {
                        int pageIdex = StockSeries?.Count ?? 0 / (PageSize == 0 ? 1 : PageSize);
                        var results = await GetStockCategoryGroupPage(pageIdex, PageSize);
                        foreach (var item in results)
                        {
                            if (StockSeries?.Count(s => s.CategoryName == item.CategoryName) == 0)
                            {
                                StockSeries.Add(item);
                            }
                        }

                        this.TotalAmount = this.StockSeries?.Select(p => p.SubCostAmount).Sum();

                        if (results.Count() == 0 || results.Count() == StockSeries.Count)
                        {
                            ItemTreshold = -1;
                            return this.StockSeries;
                        }

                    }
                    catch (Exception ex)
                    {
                        Crashes.TrackError(ex);
                        ItemTreshold = -1;
                    }

                    this.StockSeries = new ObservableRangeCollection<StockCategoryGroup>(StockSeries);
                    return this.StockSeries;
                }

            }, this.WhenAny(x => x.StockSeries, x => x.GetValue().Count > 0));

            //仓库选择
            this.StockSelected = ReactiveCommand.Create<object>(async e =>
            {
                await SelectStock((data) =>
                 {
                     Filter.WareHouseId = data.Id;
                     Filter.WareHouseName = data.Name;
                     ((ICommand)Load)?.Execute(null);
                 }, BillTypeEnum.None);
            });


            //绑定页面菜单
            _popupMenu = new PopupMenu(this, new Dictionary<MenuEnum, Action<SubMenu, ViewModelBase>>
            {
                //ZEROSTOCK
                { MenuEnum.ZEROSTOCK, (m,vm)=>{
                    ShowZero = true;
                    ((ICommand)Load)?.Execute(null);
                } },
                 //DELETEPRODUCT
                { MenuEnum.DELETEPRODUCT, (m,vm)=>{
                    Disabled = true;
                    ((ICommand)Load)?.Execute(null);
                } },
                 //SCAN
                { MenuEnum.SCAN, async (m,vm)=>{
                   await this.NavigateAsync("ScanBarcodePage", ("action", "add"));
                  } }
            });

            this.BindBusyCommand(Load);
        }


        public async Task<IList<StockCategoryGroup>> GetStockCategoryGroupPage(int pageNumber, int pageSize)
        {
            var stockCategoies = new List<StockCategoryGroup>();
            int? wareHouseId = Filter.WareHouseId;
            int? categoryId = 0;
            int? productId = 0;
            string productName = "";
            int? brandId = 0;
            bool? status = Disabled;
            int? maxStock = 0;
            bool? showZeroStack = ShowZero;
            int pagenumber = pageNumber;

            var results = await _reportingService.GetStocksAsync(wareHouseId, categoryId, productId, productName, brandId, status, maxStock, showZeroStack, pagenumber, this.ForceRefresh, new System.Threading.CancellationToken());
            if (results != null && results.Any())
            {
                foreach (var group in results.GroupBy(s => s.CategoryId))
                {
                    var firs = group.FirstOrDefault();
                    stockCategoies.Add(new StockCategoryGroup(firs?.CategoryName, group.ToList()));
                }
            }
            return stockCategoies.ToList();
        }


        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
        }

        public override void OnAppearing()
        {
            base.OnAppearing();

            //控制显示菜单
            _popupMenu?.Show(15, 16, 17);

            ((ICommand)Load)?.Execute(null);
        }

    }
}
