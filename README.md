# StockXPayoutBot
Simple console app which allows you to log into your StockX account and check the pay out, specific to your account, seller level and any current promotions on seller fees

It will read a text file in the bin folder called urls.txt, format of the information can be:

[sku] [US Size] - Will search for the SKU and return payout information for the specified size only
F36980 12.5

[stockX URL] [US Size] - Will use the stockX URL and return payout information for the specified size only

e.g. https://stockx.com/adidas-human-race-nmd-pharrell-oreo 11.5

You can also specify multiple sizes but separating them with a comma

e.g. https://stockx.com/adidas-human-race-nmd-pharrell-multi-color 9,9.5

The application will also work with streetwear clothing payout too (use "os" for one size items)

e.g. 
https://stockx.com/bape-x-adidas-riddell-helmet-green os

https://stockx.com/supreme-box-logo-crewneck-fw18-ash-grey xl

You can also return the payout for all sizes of a shoe by not specifying a size, but I woukdn't recommend as this can cause errors and cause CloudFlare protection to kick in

