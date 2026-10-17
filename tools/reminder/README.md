# Desktop reminder

At 20:00 and 22:00 a systemd user timer asks the running trainer (`GET /api/reminder`) whether today's
study is done. If not — no activity yet, or tasks left — a desktop notification appears ("Серия 5 дн.
под угрозой", tasks left, words due) with a button that opens the trainer. Nothing happens when the
trainer is not running.

Install (Linux with systemd and `notify-send`):

```bash
install -m 755 tools/reminder/kortrainer-remind ~/.local/bin/
install -m 644 tools/reminder/kortrainer-reminder.{service,timer} ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now kortrainer-reminder.timer
systemctl --user start kortrainer-reminder.service   # try it now
```

Change the times in `kortrainer-reminder.timer` (`OnCalendar=`). Set `KORTRAINER_URL` in the service if
the trainer is not on `http://localhost:8080`. On the phone the Today page shows the same reminder
banner; push notifications would need HTTPS.
