#!/usr/bin/env bash
# Freezes a user unit within a second of the laptop getting hot and thaws it once cool (argv: unit).
unit=$1
frozen=0
while systemctl --user is-active -q "$unit" || [ "$frozen" = 1 ]; do
  t=$(cat /sys/class/thermal/thermal_zone0/temp)
  if [ "$frozen" = 0 ] && [ "$t" -ge 80000 ]; then
    systemctl --user freeze "$unit" && frozen=1 && echo "$(date +%T) froze at $((t / 1000)) °C"
  elif [ "$frozen" = 1 ] && [ "$t" -le 65000 ]; then
    systemctl --user thaw "$unit" && frozen=0 && echo "$(date +%T) thawed at $((t / 1000)) °C"
  fi
  sleep 1
done
