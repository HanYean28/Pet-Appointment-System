# Stripe API Integration Setup Guide

## Overview
Your Pawfect Grooming system now has comprehensive Stripe API integration with support for:
- Credit/Debit Card payments
- FPX (Malaysian bank transfers)
- Secure payment processing with webhooks
- Payment confirmation and error handling

## Setup Steps

### 1. Get Your Stripe API Keys

1. **Create a Stripe Account**: Go to [https://stripe.com](https://stripe.com) and create an account
2. **Get API Keys**: 
   - Go to Dashboard → Developers → API Keys
   - Copy your **Publishable Key** (starts with `pk_test_` for test mode)
   - Copy your **Secret Key** (starts with `sk_test_` for test mode)

### 2. Update Configuration

Update your `appsettings.json` file with your actual Stripe keys:

```json
{
  "Stripe": {
    "PublishableKey": "pk_test_YOUR_ACTUAL_PUBLISHABLE_KEY_HERE",
    "SecretKey": "sk_test_YOUR_ACTUAL_SECRET_KEY_HERE",
    "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET_HERE"
  }
}
```

### 3. Set Up Webhooks (Important!)

1. **In Stripe Dashboard**:
   - Go to Developers → Webhooks
   - Click "Add endpoint"
   - Set endpoint URL to: `https://yourdomain.com/Payment/StripeWebhook`
   - Select these events:
     - `payment_intent.succeeded`
     - `payment_intent.payment_failed`
   - Copy the webhook signing secret

2. **For Local Development**:
   - Use Stripe CLI: `stripe listen --forward-to localhost:5000/Payment/StripeWebhook`
   - Copy the webhook secret from the CLI output

### 4. Test the Integration

1. **Test Cards** (use these in test mode):
   - Success: `4242 4242 4242 4242`
   - Decline: `4000 0000 0000 0002`
   - Requires 3D Secure: `4000 0025 0000 3155`

2. **Test Flow**:
   - Create a booking
   - Go to payment
   - Select "Stripe" as payment method
   - Use test card details
   - Complete payment

## Features Implemented

### PaymentController Methods Added:
- `Stripe(int bookingId, string paymentType)` - Shows Stripe payment form
- `CreatePaymentIntent(int bookingId, string paymentType)` - Creates Stripe payment intent
- `ConfirmPayment([FromBody] ConfirmPaymentRequest request)` - Confirms successful payments
- `StripeWebhook()` - Handles Stripe webhook events
- `HandlePaymentIntentSucceeded(PaymentIntent paymentIntent)` - Processes successful payments
- `HandlePaymentIntentFailed(PaymentIntent paymentIntent)` - Handles failed payments

### Views Created:
- `Stripe.cshtml` - Modern, secure payment form with Stripe Elements

### Database Integration:
- Payment records are automatically created
- Booking status is updated to "Completed"
- Stripe payment intent IDs are stored for tracking

## Security Features

1. **PCI Compliance**: Card details never touch your server
2. **Webhook Verification**: All webhooks are verified using Stripe signatures
3. **Error Handling**: Comprehensive error handling for all payment scenarios
4. **Idempotency**: Prevents duplicate payment processing

## Production Deployment

### Before Going Live:

1. **Switch to Live Keys**:
   - Replace test keys with live keys in `appsettings.json`
   - Update webhook endpoint to production URL

2. **Enable Required Features**:
   - Enable 3D Secure if required
   - Set up proper error monitoring
   - Configure email notifications

3. **Test Thoroughly**:
   - Test with real (small) amounts
   - Verify webhook delivery
   - Test error scenarios

## Troubleshooting

### Common Issues:

1. **"Invalid API Key"**: Check your secret key in `appsettings.json`
2. **Webhook Not Working**: Verify webhook URL and signing secret
3. **Payment Fails**: Check Stripe dashboard for detailed error logs
4. **3D Secure Issues**: Ensure your account is properly configured

### Debug Mode:
- Check Stripe dashboard logs
- Use browser developer tools to see JavaScript errors
- Check server logs for webhook processing

## Support

- **Stripe Documentation**: [https://stripe.com/docs](https://stripe.com/docs)
- **Stripe Support**: Available through your Stripe dashboard
- **Test Mode**: Use test keys and test cards for development

## Next Steps

Consider implementing:
- Payment method saving for returning customers
- Subscription payments for recurring services
- Multi-currency support
- Payment analytics and reporting
- Refund functionality
