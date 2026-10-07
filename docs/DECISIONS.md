# Design decisions

This file records the decisions that fill gaps in, or adjust, the [BRD](./BRD.md). Each one gives the reason.

The product owner made the decisions marked **(owner)**. The others are the implementer's recommended defaults, which the owner pre-approved ("go with your recommendation").

## Stack
- **(owner)** The backend is ASP.NET Core (.NET 10 LTS) with EF Core on Microsoft SQL Server. The web app is React and the mobile app is React Native (Expo).
- The backend has two projects: `Fatoura.Domain` (pure rules, no dependencies) and `Fatoura.Api` (HTTP, EF Core, PDF, Excel). At this size, separate Application and Infrastructure layers would only add interfaces with a single implementation.
- MediatR, AutoMapper and FluentAssertions are not used because they moved to commercial licences in 2025. Tests use xUnit v3 and Shouldly.

## Language
- **(owner)** The UI is bilingual (Arabic and English) with full right-to-left support for Arabic.
- Printed documents (invoice, quotation, credit note) are in English, as the BRD specifies. Arabic text in client names or descriptions still prints correctly through a fallback Arabic font.
- Digits are Latin (0-9) in both languages, as is normal for UAE business documents.

## VAT and amounts (BRD §5)
- **Tax category:** "Taxable yes/no" becomes a tax category of **Standard (5%)**, **Zero-rated** or **Exempt**. The FTA VAT return (VAT 201) reports zero-rated and exempt supplies in separate boxes, so a yes/no flag isn't enough.
- **Line math:**
  - `Line Net = round2(Qty × Unit Price)`
  - `Line VAT = round2(Line Net × rate)`
  - `Line Amount = Line Net + Line VAT`
  - Rounding is to 2 decimals, half away from zero.
- **Total VAT = Σ Line VAT.** The BRD's `Sub Total × 5%` gives the same result when every line is standard-rated, as in the reference invoice (46,000.00 → 2,300.00). Summing the lines keeps the printed "Vat" column adding up to the printed total, and it handles zero-rated and exempt lines correctly.
- **Server is authoritative:** the server recalculates every amount and ignores totals sent by a client. The web and mobile apps use the same formulas only for live previews.
- **Shared fixtures:** the C# and TypeScript implementations are tested against the same fixtures in `spec/calc-cases.json`.
- **Precision:**
  - Amounts are stored as `decimal(18,2)` and quantities as `decimal(18,3)`.
  - Unit prices have at most 2 decimals (fils).
  - Quantities have at most 3 decimals.

## Discounts
- **(owner)** A quotation or tax invoice can carry **one discount for the whole document**, entered as an amount (AED) or a percentage of the sub total. There are no per-line discounts.
- **VAT after the discount:** the FTA charges VAT on the discounted value, so the discount is spread over the lines in proportion to their amounts before VAT is worked out:
  - `Line Gross = round2(Qty × Unit Price)`; `Sub Total (before discount) = Σ Line Gross`
  - `Discount = amount`, or `round2(Sub Total × % / 100)`; it can't exceed the sub total
  - Each line's share is worked out in whole fils, and leftover fils go to the lines with the largest remainders (the earlier line on a tie), so the shares add up exactly to the discount
  - `Line Net = Line Gross − share`, then `Line VAT = round2(Line Net × rate)` as before
- **Stored totals:** `SubTotal` is the amount **after** the discount, so reports, P&L and the VAT return need no change. The document also stores the discount as entered and its amount; each line stores its share.
- **Printed:** when there is a discount, the line table gains a **Discount** column. The totals show Sub Total, Discount, Total excl. VAT, VAT and Total Amount, so the invoice states the discount as the FTA requires.
- **Credit notes** take back the matching share of the line's discount, in proportion to the quantity credited. The credit that finishes a line takes exactly what is left, so a fully credited line returns its whole discount.
- **Conversion:** converting a quotation carries its discount to the invoice.
- **Purchases** have no discount field: enter supplier invoices at their net prices.
- Fixtures for the discount math are in `spec/calc-cases.json`, shared by the C# and TypeScript tests.

## TRN validation
- **Rule:** a TRN must be exactly 15 digits **starting with "10"**. Spaces and dashes are ignored.
- **Why not "100":** the BRD says TRNs "usually start with 100". But both TRNs in the reference invoice (`105386581000003` for the company, `105325228200003` for the client) start with `105`, so requiring `100` would reject real, valid TRNs.
- **Required where:** the company TRN is required. A client's TRN is optional, because walk-in consumers are not VAT-registered, but it is validated whenever it is entered.

## Document numbers
- **Configurable pattern:** each document type (Invoice, Quotation, Credit Note, Purchase) has its own pattern, made of these tokens plus any literal text:
  - `{YYYY}`, `{YY}`, `{MM}`, `{MON}`, `{DD}`
  - `{SEQ}` or `{SEQ:n}`
- **Defaults:** `INV/{MON}/{YY}{SEQ:4}` (for example `INV/SEP/260001`), `QUO/…`, `CN/…` and `PUR/…`, each resetting yearly. The reference number `RIHM/AUG/260819` can be reproduced with `RIHM/{MON}/{YY}{MM}{SEQ:2}`.
- **Gap-free counters:** numbers come from a counter per (document type, reset period) that is incremented atomically inside the same transaction that saves the document. The FTA requires sequential, unique tax invoice numbers.
- **No invoice drafts:** an invoice gets its number only when it is issued. Quotations serve as drafts.

## Deleting and editing invoices
- **(owner)** Admin "delete" of an issued invoice is a **Void**:
  - The invoice keeps its number and must have a reason.
  - It prints with a VOID watermark and is excluded from all totals and reports.
  - Its stock is returned.
  - The action is written to the audit log.
  - This keeps the number sequence free of gaps, as the FTA requires.
- **Admin edits:** an Admin may edit an issued invoice. The number and the issue snapshot stay the same, totals are recalculated, stock is re-applied, and the change is audited.
- **Cashiers** can never edit or void an invoice. They can issue a **Tax Credit Note** against it (BRD §2).

## Cashier visibility
- **(owner)** Cashiers can view all invoices and quotations read-only, for example to reprint them or to issue a credit note. Their dashboard shows only their own sales.
- **"Shift sales":** a cashier's own invoices for the current calendar day in Asia/Dubai time.

## Stock
- **(owner)** Products can track stock:
  - Purchases add to stock.
  - Invoices take from stock.
  - Credit notes return stock when "return to stock" is ticked.
  - Voids return stock.
  - Admin adjustments are recorded with a reason.
- **(owner)** If an invoice would take stock below zero, the user sees a **warning but the sale is allowed** by default. A Settings toggle can switch this to block the sale.
- **Ledger:** the `StockMovements` table is the record of truth. `Items.StockQty` is a cached value updated atomically in the same transaction.
- **Low-stock alert:** shown on the Admin dashboard when `StockQty ≤ ReorderLevel`.

## Payments
- **Why payments exist:** the BRD's "open invoices" count needs a balance, so invoices record payments (amount, date, method: Cash, Card, Bank Transfer or Cheque).
- **Balance:** Total − credit notes − payments.
- **Default:** a new invoice defaults to "paid in full – Cash" so simple counter sales stay one click.

## Reports
- **Profit & Loss** follows the BRD: net sales excluding VAT (invoices − credit notes) − purchases excluding VAT. Each invoice line also stores its cost at the time of sale, so a cost-of-goods P&L can be added later without migrating data.
- **VAT report** lists output VAT (invoices − credit notes) and input VAT (purchases), grouped like the VAT 201 return: standard-rated, zero-rated and exempt supplies, standard-rated expenses, and net VAT payable. Box 1 is split by the company's emirate.

## Company settings
- **Address and emirate added:** the BRD's company fields gain an **address** and an **emirate**. UAE tax invoices must show the supplier's address, and the VAT return splits sales by emirate. The address prints in the header block.
- **Stamp image:** a company stamp image can be uploaded and is printed in the "Stamp & Signature" area. Without one, an empty box is printed for a physical stamp.

## PDF
- **(owner)** PDFs are produced on the server with QuestPDF (Community licence: free for organisations with under USD 1M annual gross revenue). Larger organisations need a QuestPDF commercial licence.
- **Layout:** matches the reference invoice: US Letter landscape, a Calibri-compatible font (Carlito, OFL), the same blocks, table columns and footer order.
- **Same PDF everywhere:** the web and mobile apps both download the PDF from the server, so every copy is identical.

## Out of scope for v1
- Multi-currency, and FTA e-invoicing (PINT-AE through an accredited provider).
- **(owner)** Settings and user management stay web-only. The mobile app covers daily operations, the dashboard and read-only reports.

## Implementation details
- **Who can change what:**
  - Cashiers can **add** clients but not edit or delete them (BRD §2).
  - Contacts and items that appear on documents are **deactivated instead of deleted**, so history stays intact.
  - Quotations can be changed by the cashier who created them or by an Admin, until they are converted.
- **Invoice dates:** cashiers always issue invoices dated today (Asia/Dubai). Admins may back-date an invoice but never post-date it.
- **After a credit note:** an invoice that has a credit note can no longer be edited or voided. Credit notes always use the original line's price, tax category and VAT rate.
- **Payments:** a payment cannot exceed the balance due.
- **Purchases:** purchases are internal records (they don't use FTA invoice numbering), so Admins can edit or delete them. The stock they added is reversed. The moving-average cost is not recalculated backwards.
- **Purchase VAT:** VAT on purchases is calculated with the same per-line rule as sales. If a supplier's invoice differs by a fils because of its own rounding, adjust a line amount.
- **Reports:**
  - Each report covers at most 5 years.
  - Cashiers can run the Sales report, and it always shows only their own sales.
  - Purchases, P&L and VAT reports are Admin-only.
  - Reports export to Excel (.xlsx).
- **Repository layout:** the mobile app lives in `/mobile`, outside the `clients/` npm workspace, so it has its own React Native dependency tree. It shares `@fatoura/shared` through Metro and tsconfig settings.
- **Translations:** the Arabic and English dictionaries live in `@fatoura/shared`, so the web and mobile apps use the same wording.
- **Security:**
  - Login is rate-limited per client IP (20 per minute by default). Each account also locks for 15 minutes after 5 wrong passwords.
  - Refresh tokens rotate on every use, and replaying an old one revokes the whole login.
  - A replay within 30 seconds of rotation is accepted instead. This happens when two tabs refresh at the same moment, and accepting it avoids logging users out.
  - The API trusts `X-Forwarded-For` from the nginx container in front of it. Don't expose the API port directly.
