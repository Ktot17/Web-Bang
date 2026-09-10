#!/bin/bash
# scripts/notify.sh - Уведомление о статусе пайплайна в Telegram

# Переменные окружения из GitLab CI
PROJECT_ID="${CI_PROJECT_ID}"
PIPELINE_ID="${CI_PIPELINE_ID}"
CI_SERVER_URL="${CI_SERVER_URL}"
GITLAB_TOKEN="${GITLAB_TOKEN}"
TELEGRAM_BOT_TOKEN="${TELEGRAM_BOT_TOKEN}"
TELEGRAM_CHAT_ID="${TELEGRAM_CHAT_ID}"
COMMIT_BRANCH="${CI_MERGE_REQUEST_SOURCE_BRANCH_NAME}"
COMMIT_SHA="${CI_COMMIT_SHORT_SHA}"
PROJECT_NAME="${CI_PROJECT_NAME}"
PIPELINE_URL="${CI_PROJECT_URL}/-/pipelines/${PIPELINE_ID}"

# Функция получения информации о пайплайне через GitLab API
get_pipeline_info() {
    local url="${CI_SERVER_URL}/api/v4/projects/${PROJECT_ID}/pipelines/${PIPELINE_ID}"
    curl -s --header "PRIVATE-TOKEN: ${GITLAB_TOKEN}" "${url}"
}

# Функция получения информации о джобах через GitLab API
get_jobs_info() {
    local url="${CI_SERVER_URL}/api/v4/projects/${PROJECT_ID}/pipelines/${PIPELINE_ID}/jobs"
    curl -s --header "PRIVATE-TOKEN: ${GITLAB_TOKEN}" "${url}"
}

# Функция форматирования сообщения для Telegram
format_message() {
    local pipeline_json="$1"
    local jobs_json="$2"
    local status_emoji=""
    local overall_status=""
    
    # Получаем статус из pipeline
    overall_status=$(echo "$pipeline_json" | jq -r '.status // "unknown"')
    
    case "$overall_status" in
        "success")
            status_emoji="✅"
            ;;
        "failed")
            status_emoji="❌"
            ;;
        "running")
            status_emoji="🔄"
            ;;
        "pending")
            status_emoji="⏳"
            ;;
        "canceled")
            status_emoji="⏹️"
            ;;
        "skipped")
            status_emoji="⏭️"
            ;;
        *)
            status_emoji="❓"
            ;;
    esac
    
    local message=""
    message+="${status_emoji} *CI/CD Pipeline Report* ${status_emoji}\n"
    message+="━━━━━━━━━━━━━━━━━━━━━━━━\n"
    message+="📦 *Проект:* ${PROJECT_NAME}\n"
    message+="🌿 *Ветка:* ${COMMIT_BRANCH:-unknown}\n"
    message+="🔖 *Коммит:* \`${COMMIT_SHA}\`\n"
    message+="🔗 *Пайплайн:* [Ссылка](${PIPELINE_URL})\n"
    message+="━━━━━━━━━━━━━━━━━━━━━━━━\n"
    message+="📊 *Статус:* ${status_emoji} ${overall_status^^}\n"
    message+="━━━━━━━━━━━━━━━━━━━━━━━━\n"
    message+="📋 *Джобы:*\n"
    
    # Проверяем, что jobs_json - это массив
    local is_array=$(echo "$jobs_json" | jq 'if type=="array" then true else false end')
    
    if [ "$is_array" = "true" ]; then
        local jobs_count=$(echo "$jobs_json" | jq '. | length')
        
        if [ "$jobs_count" -eq 0 ]; then
            message+="  Нет джоб\n"
        else
            for i in $(seq 0 $((jobs_count - 1))); do
                local name=$(echo "$jobs_json" | jq -r ".[$i].name // \"unknown\"")
                local status=$(echo "$jobs_json" | jq -r ".[$i].status // \"unknown\"")
                local stage=$(echo "$jobs_json" | jq -r ".[$i].stage // \"unknown\"")
                
                local emoji=""
                case "$status" in
                    "success") emoji="✅" ;;
                    "failed") emoji="❌" ;;
                    "running") emoji="🔄" ;;
                    "pending") emoji="⏳" ;;
                    "manual") emoji="✋" ;;
                    "skipped") emoji="⏭️" ;;
                    "canceled") emoji="⏹️" ;;
                    *) emoji="❓" ;;
                esac
                
                message+="  ${emoji} *${name}* \`(${stage})\`\n"
                message+="     Статус: ${status}\n"
            done
        fi
    else
        message+="  Не удалось получить список джоб\n"
        message+="  Ошибка API\n"
    fi
    
    message+="━━━━━━━━━━━━━━━━━━━━━━━━\n"
    
    printf "%b" "$message"
}

# Функция отправки в Telegram
send_telegram() {
    local message="$1"
    local url="https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendMessage"
    
    # Экранируем специальные символы для JSON
    local escaped_message=$(echo "$message" | jq -Rs .)
    
    curl -s -X POST "${url}" \
        -H "Content-Type: application/json" \
        -d "{
            \"chat_id\": \"${TELEGRAM_CHAT_ID}\",
            \"text\": ${escaped_message},
            \"parse_mode\": \"Markdown\",
            \"disable_web_page_preview\": true
        }" > /dev/null
}

# Основная логика
main() {
    echo "=== Сбор информации о пайплайне ==="
    local pipeline_json=$(get_pipeline_info)
    
    if [ -z "$pipeline_json" ] || [ "$pipeline_json" = "null" ]; then
        echo "Ошибка: не удалось получить информацию о пайплайне"
        exit 1
    fi
    
    echo "=== Сбор информации о джобах ==="
    local jobs_json=$(get_jobs_info)
    
    if [ -z "$jobs_json" ] || [ "$jobs_json" = "null" ]; then
        echo "Ошибка: не удалось получить информацию о джобах"
        exit 1
    fi
    
    echo "=== Форматирование сообщения ==="
    local message=$(format_message "$pipeline_json" "$jobs_json")
    echo "$message"
    
    echo "=== Отправка в Telegram ==="
    if [ -n "$TELEGRAM_BOT_TOKEN" ] && [ -n "$TELEGRAM_CHAT_ID" ]; then
        send_telegram "$message"
        echo "✅ Уведомление отправлено в Telegram"
    else
        echo "❌ Не настроены переменные TELEGRAM_BOT_TOKEN или TELEGRAM_CHAT_ID"
        echo "TELEGRAM_BOT_TOKEN: ${TELEGRAM_BOT_TOKEN:+set}"
        echo "TELEGRAM_CHAT_ID: ${TELEGRAM_CHAT_ID:+set}"
        exit 1
    fi
    
    echo "=== Готово ==="
}

# Запуск
main