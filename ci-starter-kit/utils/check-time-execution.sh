#!/bin/sh
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

killTimeout=${KILL_TIMEOUT:-"120"} #sec

maxTimeExecution=$1; shift
cmd="$@"

TIMEOUT_EXIT_CODE=124

begin=$(date +"%s")
timeout -k $killTimeout $maxTimeExecution $cmd
CMD_EXIT_CODE=`echo $?`

if [ $CMD_EXIT_CODE -eq $TIMEOUT_EXIT_CODE ] ; then
  printf "${RED}ERROR:${NC} command \"$cmd\" was stoped by timeout ($(($maxTimeExecution / 60)) minutes $(($maxTimeExecution % 60)) seconds) \n"
  exit $CMD_EXIT_CODE
fi

if [ $CMD_EXIT_CODE -ne 0 ] ; then
  printf "${RED}ERROR:${NC} command \"$cmd\" return exit code: $CMD_EXIT_CODE\n"
  exit $CMD_EXIT_CODE
fi

termin=$(date +"%s")
currTimeExecution=$(($termin-$begin))

printf "${GREEN}INFO:${NC} $(($currTimeExecution / 60)) minutes and $(($currTimeExecution % 60)) seconds elapsed for \"$cmd\"\n"
printf "${GREEN}INFO:${NC} it's ok, max time execution ($(($maxTimeExecution / 60)) minutes $(($maxTimeExecution % 60)) seconds) not exeeded.\n"

exit $CMD_EXIT_CODE
