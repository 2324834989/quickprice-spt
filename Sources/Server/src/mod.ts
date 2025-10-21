import type { DependencyContainer } from "tsyringe";
import type { StaticRouterModService } from "@spt/services/mod/staticRouter/StaticRouterModService";
import type { DatabaseServer } from "@spt/servers/DatabaseServer";
import type { DatabaseService } from "@spt/services/DatabaseService";
import type { RagfairOfferService } from "@spt/services/RagfairOfferService";
import type { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { MemberCategory } from "@spt/models/enums/MemberCategory";
import { CurrencyPurchasePrices } from "models/currencyPurchasePrices"

class Mod implements IPreSptLoadMod
{
    private readonly modVersion = "1.8.0";

    private container: DependencyContainer;
    private logger: ILogger;

    public preSptLoad(container: DependencyContainer): void
    {
        this.container = container;
        this.logger = container.resolve<ILogger>("WinstonLogger");

        const staticRouterModService = this.container.resolve<StaticRouterModService>("StaticRouterModService");

        staticRouterModService.registerStaticRouter(
            "ShowMeTheMoneyRoutes-GetCurrencyPurchasePrices",
            [
                {
                    url: "/showMeTheMoney/getCurrencyPurchasePrices",

                    action: (url, info, sessionId, output) =>
                    {
                        return new Promise((resolve) =>
                        {
                            const result = JSON.stringify(this.getCurrencyPurchasePrices());
                            resolve(result);
                        });
                    }
                }
            ],
            "Static-ShowMeTheMoneyRoutes"
        );

        staticRouterModService.registerStaticRouter(
            "ShowMeTheMoneyRoutes-GetStaticPriceTable",
            [
                {
                    url: "/showMeTheMoney/getStaticPriceTable",

                    action: (url, info, sessionId, output) =>
                    {
                        return new Promise((resolve) =>
                        {
                            const result = JSON.stringify(this.getStaticPriceTable());
                            resolve(result);
                        });
                    }
                }
            ],
            "Static-ShowMeTheMoneyRoutes"
        );

        staticRouterModService.registerStaticRouter(
            "ShowMeTheMoneyRoutes-GetDynamicPriceTable",
            [
                {
                    url: "/showMeTheMoney/getDynamicPriceTable",

                    action: (url, info, sessionId, output) =>
                    {
                        return new Promise((resolve) =>
                        {
                            const result = JSON.stringify(this.getDynamicPriceTable());
                            resolve(result);
                        });
                    }
                }
            ],
            "Static-ShowMeTheMoneyRoutes"
        );

        this.logInfo("Static routes hooked up. Ready to make some money...");
    }

    private getCurrencyPurchasePrices(): CurrencyPurchasePrices
    {
        const databaseService = this.container.resolve<DatabaseService>("DatabaseService");
        const peacekeeper = databaseService.getTrader("5935c25fb3acc3127c3d8cd9");
        const skier = databaseService.getTrader("58330581ace78e27b8b10cee");

        const eurPrice = skier.assort.barter_scheme["677536ee7949f87882036fb0"][0][0].count;
        const usdPrice = peacekeeper.assort.barter_scheme["676d24a5798491c5260f4b01"][0][0].count;

        const result: CurrencyPurchasePrices = { eur: eurPrice, usd: usdPrice };

        return result;
    }

    private getStaticPriceTable(): Record<string, number>
    {
        const databaseServer = this.container.resolve<DatabaseServer>("DatabaseServer");

        const handbookTable = databaseServer.getTables().templates.handbook;
        const itemTable = databaseServer.getTables().templates.items;
        const priceTable = databaseServer.getTables().templates.prices;

        var clonedPriceTable: Record<string, number> = {};
        for (const [itemId, templateItem] of Object.entries(itemTable))
        {
            if (templateItem._props?.CanSellOnRagfair === true)
            {
                var price = priceTable[itemId];
                if (!price)
                {
                    price = handbookTable.Items.find(x => x.Id === itemId)?.Price ?? null;
                }

                if (price)
                    clonedPriceTable[itemId] = price;
            }
        }

        return clonedPriceTable;
    }

    private getDynamicPriceTable(): Record<string, number>
    {
        const ragfairOfferService = this.container.resolve<RagfairOfferService>("RagfairOfferService");
        const priceTable = this.getStaticPriceTable();

        for (const [templateId, price] of Object.entries(priceTable))
        {
            var averageOffersPrice = 0;
            var countedOffers = 0;

            const ragfairOffersForType = ragfairOfferService.getOffersOfType(templateId);
            if (ragfairOffersForType != null)
                ragfairOffersForType.forEach((offer) =>
                {
                    if (offer.sellResult == null && offer.user.memberType != MemberCategory.TRADER)
                    {
                        averageOffersPrice += offer.requirementsCost;
                        ++countedOffers;
                    }
                });

            if (averageOffersPrice > 0)
            {
                priceTable[templateId] = averageOffersPrice / countedOffers;
            }
            else
            {
                priceTable[templateId] = price;
            }
        }

        return priceTable;
    }

    private logInfo(message: string): void
    {
        this.logger.info(`[Show Me The Money v${this.modVersion}] ${message}`);
    }
}

export const mod = new Mod();